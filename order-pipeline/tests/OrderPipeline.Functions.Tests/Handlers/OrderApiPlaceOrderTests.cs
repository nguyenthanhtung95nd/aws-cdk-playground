using System.Net;
using System.Text.Json;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OrderPipeline.Domain.Common;
using OrderPipeline.Domain.Orders;
using OrderPipeline.Domain.Ports;
using OrderPipeline.Functions.Handlers;
using OrderPipeline.Functions.Tests.Fakes;

namespace OrderPipeline.Functions.Tests.Handlers;

public class OrderApiPlaceOrderTests
{
    private const string OrderId = "11111111-1111-1111-1111-111111111111";
    private const string PlacedAt = "2026-09-24T08:00:00Z";
    private const string ValidBody =
        """{"customerName":"Nguyen Van A","product":"Laptop","amount":1500,"notes":"none"}""";

    private readonly IOrderStore _orderStore = Substitute.For<IOrderStore>();
    private readonly OrderApi _sut;

    public OrderApiPlaceOrderTests()
    {
        _sut = new OrderApi(
            _orderStore,
            new OrderThresholds(1_000m, 5_000m),
            new FixedClock(DateTimeOffset.Parse(PlacedAt)),
            new FixedOrderIdGenerator(OrderId),
            NullLogger<OrderApi>.Instance);

        _orderStore.PlaceAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
            .Returns(call => Result<Order>.Success(call.Arg<Order>()));
    }

    [Fact]
    public async Task PlaceOrder_WhenTheOrderIsValid_ReturnsCreatedWithTheOrderId()
    {
        var result = await Place(ValidBody);

        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        var payload = Wire(result);
        Assert.Equal(OrderId, payload.GetProperty("body").GetString()!.AsJson().GetProperty("orderId").GetString());
        Assert.Equal($"/orders/{OrderId}", payload.GetProperty("headers").GetProperty("location").GetString());
    }

    [Fact]
    public async Task PlaceOrder_WhenValidationFails_ReturnsBadRequestAndStoresNothing()
    {
        var result = await Place("""{"customerName":"A","product":"","amount":1500,"notes":""}""");

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(
            "product is required.",
            Wire(result).GetProperty("body").GetString()!.AsJson().GetProperty("message").GetString());
        await _orderStore.DidNotReceive().PlaceAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlaceOrder_WhenTheStoreFails_ReturnsInternalServerError()
    {
        _orderStore.PlaceAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
            .Returns(Result<Order>.Failure("The table is not reachable."));

        var result = await Place(ValidBody);

        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task PlaceOrder_WhenTheSameBodyIsSentTwice_ProducesAByteIdenticalResponse()
    {
        var first = Wire(await Place(ValidBody)).GetRawText();
        var second = Wire(await Place(ValidBody)).GetRawText();

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task PlaceOrder_WhenTheOrderIsValid_StoresItWithTheInjectedClockAndId()
    {
        await Place(ValidBody);

        var stored = (Order)_orderStore.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IOrderStore.PlaceAsync))
            .GetArguments()[0]!;

        Assert.Equal(OrderId, stored.OrderId);
        Assert.Equal(DateTimeOffset.Parse(PlacedAt), stored.PlacedAt);
    }

    [Fact]
    public async Task PlaceOrder_WhenTheLambdaIsAlmostOutOfTime_HandsTheStoreACancelledToken()
    {
        await Place(ValidBody, remaining: TimeSpan.FromMilliseconds(1));

        Assert.True(TokenGivenToTheStore().IsCancellationRequested);
    }

    [Fact]
    public async Task PlaceOrder_WhenThereIsTimeLeft_HandsTheStoreALiveToken()
    {
        await Place(ValidBody, remaining: TimeSpan.FromSeconds(30));

        Assert.False(TokenGivenToTheStore().IsCancellationRequested);
    }

    private CancellationToken TokenGivenToTheStore() =>
        (CancellationToken)_orderStore.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IOrderStore.PlaceAsync))
            .GetArguments()[1]!;

    private Task<IHttpResult> Place(string body, TimeSpan? remaining = null)
    {
        var context = Substitute.For<ILambdaContext>();
        context.RemainingTime.Returns(remaining ?? TimeSpan.FromSeconds(30));

        return _sut.PlaceOrder(body, new APIGatewayHttpApiV2ProxyRequest
        {
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                RequestId = "req-fixed",
            },
        }, context);
    }

    private static JsonElement Wire(IHttpResult result) => HttpResultWire.Of(result);
}

file static class JsonTextExtensions
{
    public static JsonElement AsJson(this string text) => JsonDocument.Parse(text).RootElement.Clone();
}
