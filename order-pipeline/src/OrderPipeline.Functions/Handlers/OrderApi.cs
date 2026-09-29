using System.Globalization;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Microsoft.Extensions.Logging;
using OrderPipeline.Contracts;
using OrderPipeline.Domain.Common;
using OrderPipeline.Domain.Orders;
using OrderPipeline.Domain.Ports;
using OrderPipeline.Functions.Observability;
using OrderPipeline.Functions.Responses;

namespace OrderPipeline.Functions.Handlers;

public class OrderApi(
    IOrderStore orderStore,
    OrderThresholds thresholds,
    IClock clock,
    IOrderIdGenerator orderIds,
    ILogger<OrderApi> logger)
{
        [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, ApiRoutes.Orders)]
    public async Task<IHttpResult> PlaceOrder(
        [FromBody] string body,
        APIGatewayHttpApiV2ProxyRequest request,
        ILambdaContext context)
    {
        var correlationId = request.RequestContext.RequestId;
        using var correlation = logger.BeginScope(LogScope.Correlation(correlationId));

        return Traceable(await Place(body, context), correlationId);
    }

    private async Task<IHttpResult> Place(string body, ILambdaContext context)
    {
        var command = PlaceOrderCommand.Parse(body);
        if (!command.IsSuccess)
        {
            logger.LogInformation("Order refused before it was stored. {Reason}", command.Error);
            return HttpResults.BadRequest(new ErrorResponse(command.Error!));
        }

        var order = Order.Place(orderIds.Next(), command.Value!, thresholds, clock.UtcNow);

        using var span = OrderActivity.Begin(order.OrderId);
        using var deadline = LambdaDeadline.For(context);
        var placed = await orderStore.PlaceAsync(order, deadline.Token);
        if (!placed.IsSuccess)
        {
            logger.LogError("Could not store order {OrderId}. {Reason}", order.OrderId, placed.Error);
            return HttpResults.InternalServerError();
        }

        logger.LogInformation(
            "Order {OrderId} accepted for {Amount} and marked high value: {IsHighValue}",
            order.OrderId,
            order.Amount,
            order.IsHighValue);

        return HttpResults.Created(
            $"{ApiRoutes.Orders}/{order.OrderId}",
            new PlaceOrderResponse(order.OrderId, order.Status.ToWireFormat()));
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ApiRoutes.Orders)]
    public async Task<IHttpResult> ListOrders(
        APIGatewayHttpApiV2ProxyRequest request,
        ILambdaContext context)
    {
        var correlationId = request.RequestContext.RequestId;
        using var correlation = logger.BeginScope(LogScope.Correlation(correlationId));

        return Traceable(await List(request, context), correlationId);
    }

    private async Task<IHttpResult> List(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var asked = Requested(request);
        if (!asked.IsSuccess)
        {
            logger.LogInformation("Order listing refused. {Reason}", asked.Error);
            return HttpResults.BadRequest(new ErrorResponse(asked.Error!));
        }

        using var deadline = LambdaDeadline.For(context);
        var page = await orderStore.ListAsync(asked.Value, deadline.Token);
        if (!page.IsSuccess)
        {
            logger.LogError("Could not list orders. {Reason}", page.Error);
            return HttpResults.InternalServerError();
        }

        return HttpResults.Ok(new OrderPageResponse(
            page.Value!.Orders.Select(ToResponse).ToArray(),
            page.Value.ResumeFrom));
    }

    private static Result<OrderPageRequest> Requested(APIGatewayHttpApiV2ProxyRequest request)
    {
        var parameters = request.QueryStringParameters;

        if (parameters is null)
        {
            return Result<OrderPageRequest>.Success(OrderPageRequest.First);
        }

        parameters.TryGetValue(ApiParameters.Cursor, out var cursor);

        if (!parameters.TryGetValue(ApiParameters.Limit, out var limit) || limit is not { Length: > 0 })
        {
            return OrderPageRequest.For(null, cursor);
        }

        return int.TryParse(limit, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)
            ? OrderPageRequest.For(size, cursor)
            : Result<OrderPageRequest>.Failure($"{ApiParameters.Limit} must be a whole number.");
    }

    private static IHttpResult Traceable(IHttpResult result, string correlationId)
    {
        result.AddHeader(ApiHeaders.CorrelationId, correlationId);
        return result;
    }

    private static OrderResponse ToResponse(Order order) => new(
        order.OrderId,
        order.CustomerName,
        order.Product,
        order.Amount,
        order.Notes,
        order.Status.ToWireFormat(),
        order.IsHighValue,
        order.PlacedAt.ToString("O"));
}
