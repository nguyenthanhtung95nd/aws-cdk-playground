using System.Net;
using System.Text.Json;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OrderPipeline.Contracts;
using OrderPipeline.Domain.Common;
using OrderPipeline.Domain.Orders;
using OrderPipeline.Domain.Ports;
using OrderPipeline.Functions.Handlers;
using OrderPipeline.Functions.Tests.Fakes;

namespace OrderPipeline.Functions.Tests.Handlers;

public class OrderApiListOrdersTests
{
    private static readonly DateTimeOffset PlacedAt = new(2026, 9, 24, 8, 30, 0, TimeSpan.Zero);

    private readonly IOrderStore _orderStore = Substitute.For<IOrderStore>();
    private readonly OrderApi _sut;

    public OrderApiListOrdersTests()
    {
        _sut = new OrderApi(
            _orderStore,
            new OrderThresholds(1_000m, 5_000m),
            new FixedClock(PlacedAt),
            new FixedOrderIdGenerator("unused"),
            NullLogger<OrderApi>.Instance);
    }

    [Fact]
    public async Task ListOrders_WhenTheStoreAnswers_ReturnsOkWithEveryOrder()
    {
        StoreHolds(AnOrder("order-1"), AnOrder("order-2"));

        var result = await _sut.ListOrders(Request, Context);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(2, Orders(result).GetArrayLength());
    }

    [Fact]
    public async Task ListOrders_WhenTheStoreFails_ReturnsInternalServerError()
    {
        StoreFails("The table is not reachable.");

        var result = await _sut.ListOrders(Request, Context);

        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task ListOrders_WhenTheStoreIsEmpty_ReturnsAnEmptyArrayRatherThanNothing()
    {
        StoreHolds();

        var result = await _sut.ListOrders(Request, Context);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(0, Orders(result).GetArrayLength());
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ListOrders_WhateverTheOutcome_ReturnsTheCorrelationIdAsAHeader(HttpStatusCode expected)
    {
        if (expected == HttpStatusCode.OK)
        {
            StoreHolds(AnOrder("order-1"));
        }
        else
        {
            StoreFails("gone");
        }

        var result = await _sut.ListOrders(Request, Context);

        Assert.Equal(expected, result.StatusCode);
        Assert.Equal(
            "req-fixed",
            Wire(result).GetProperty("headers").GetProperty(ApiHeaders.CorrelationId).GetString());
    }

    [Fact]
    public async Task ListOrders_Always_SendsExactlyTheEightFieldsTheClientDecodes()
    {
        StoreHolds(AnOrder("order-1"));

        var order = Orders(await _sut.ListOrders(Request, Context))[0];

        Assert.Equal(
            ["orderId", "customerName", "product", "amount", "notes", "status", "isHighValue", "placedAt"],
            order.EnumerateObject().Select(field => field.Name));
    }

    [Fact]
    public async Task ListOrders_Always_RendersStatusInWireFormatAndPlacedAtAsRoundTrip()
    {
        StoreHolds(AnOrder("order-1"));

        var order = Orders(await _sut.ListOrders(Request, Context))[0];

        Assert.Equal("PENDING", order.GetProperty("status").GetString());
        Assert.Equal("2026-09-24T08:30:00.0000000+00:00", order.GetProperty("placedAt").GetString());
        Assert.Equal(1_500m, order.GetProperty("amount").GetDecimal());
        Assert.True(order.GetProperty("isHighValue").GetBoolean());
    }

    [Fact]
    public async Task ListOrders_WithoutAskingForAnything_TakesTheDefaultPage()
    {
        StoreHolds();

        await _sut.ListOrders(Request, Context);

        Assert.Equal(OrderPageRequest.DefaultSize, AskedFor().Size);
        Assert.Null(AskedFor().ResumeFrom);
    }

    [Fact]
    public async Task ListOrders_WithALimitAndACursor_PassesBothToTheStoreUntouched()
    {
        StoreHolds();

        await _sut.ListOrders(RequestAsking(limit: "10", cursor: "opaque"), Context);

        Assert.Equal(10, AskedFor().Size);
        Assert.Equal("opaque", AskedFor().ResumeFrom);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("-1")]
    [InlineData("many")]
    public async Task ListOrders_WithALimitTheApiWillNotServe_RefusesWithoutAskingTheStore(string limit)
    {
        StoreHolds();

        var result = await _sut.ListOrders(RequestAsking(limit: limit), Context);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        await _orderStore.DidNotReceive().ListAsync(
            Arg.Any<OrderPageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListOrders_WhenMoreOrdersRemain_TellsTheClientWhereToResume()
    {
        StorePage(new OrderPage([AnOrder("order-1")], "next-please"));

        var body = Body(await _sut.ListOrders(Request, Context));

        Assert.Equal("next-please", body.GetProperty("nextCursor").GetString());
    }

    // The Lambda serializer drops null properties, so "no more orders" reaches the client as an
    // absent field rather than a null one. The decoder has to read it that way round.
    [Fact]
    public async Task ListOrders_WhenNothingRemains_LeavesTheCursorOutEntirely()
    {
        StoreHolds(AnOrder("order-1"));

        var body = Body(await _sut.ListOrders(Request, Context));

        Assert.False(body.TryGetProperty("nextCursor", out _));
    }

    [Fact]
    public async Task ListOrders_Always_GivesTheStoreATokenTheLambdaDeadlineCanCancel()
    {
        StoreHolds();

        await _sut.ListOrders(Request, ContextWith(TimeSpan.FromMilliseconds(1)));

        var token = (CancellationToken)_orderStore.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IOrderStore.ListAsync))
            .GetArguments()[1]!;

        Assert.True(token.IsCancellationRequested);
    }

    private void StoreHolds(params Order[] orders) => StorePage(new OrderPage(orders, null));

    private void StorePage(OrderPage page) =>
        _orderStore.ListAsync(Arg.Any<OrderPageRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderPage>.Success(page));

    private void StoreFails(string reason) =>
        _orderStore.ListAsync(Arg.Any<OrderPageRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderPage>.Failure(reason));

    private OrderPageRequest AskedFor() =>
        (OrderPageRequest)_orderStore.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IOrderStore.ListAsync))
            .GetArguments()[0]!;

    private static Order AnOrder(string orderId) => new()
    {
        OrderId = orderId,
        CustomerName = "Alice",
        Product = "Headphones",
        Amount = 1_500m,
        Notes = "Express",
        Status = OrderStatus.Pending,
        IsHighValue = true,
        PlacedAt = PlacedAt
    };

    private static APIGatewayHttpApiV2ProxyRequest Request => new()
    {
        RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
        {
            RequestId = "req-fixed",
        },
    };

    private static APIGatewayHttpApiV2ProxyRequest RequestAsking(string? limit = null, string? cursor = null)
    {
        var asked = new Dictionary<string, string>();

        if (limit is not null)
        {
            asked[ApiParameters.Limit] = limit;
        }

        if (cursor is not null)
        {
            asked[ApiParameters.Cursor] = cursor;
        }

        var request = Request;
        request.QueryStringParameters = asked;
        return request;
    }

    private static ILambdaContext Context => ContextWith(TimeSpan.FromSeconds(30));

    private static ILambdaContext ContextWith(TimeSpan remaining)
    {
        var context = Substitute.For<ILambdaContext>();
        context.RemainingTime.Returns(remaining);
        return context;
    }

    private static JsonElement Orders(IHttpResult result) => Body(result).GetProperty("orders");

    private static JsonElement Body(IHttpResult result) =>
        JsonDocument.Parse(Wire(result).GetProperty("body").GetString()!).RootElement.Clone();

    private static JsonElement Wire(IHttpResult result) => HttpResultWire.Of(result);
}
