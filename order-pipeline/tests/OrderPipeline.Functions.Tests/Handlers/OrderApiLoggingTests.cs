using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Serialization.SystemTextJson;
using NSubstitute;
using OrderPipeline.Contracts;
using OrderPipeline.Domain.Common;
using OrderPipeline.Domain.Orders;
using OrderPipeline.Domain.Ports;
using OrderPipeline.Functions.Handlers;
using OrderPipeline.Functions.Tests.Fakes;

namespace OrderPipeline.Functions.Tests.Handlers;

public class OrderApiLoggingTests
{
    private const string CustomerName = "Nguyen Van A";
    private const string Notes = "delivers to 12 Le Loi, call 0900123456";

    private readonly IOrderStore _orderStore = Substitute.For<IOrderStore>();
    private readonly RecordingLogger<OrderApi> _logger = new();
    private readonly OrderApi _sut;

    public OrderApiLoggingTests()
    {
        _sut = new OrderApi(
            _orderStore,
            new OrderThresholds(1_000m, 5_000m),
            new FixedClock(DateTimeOffset.Parse("2026-09-24T08:00:00Z")),
            new FixedOrderIdGenerator("11111111-1111-1111-1111-111111111111"),
            _logger);
        AcceptEveryOrder();
    }

    [Fact]
    public async Task PlaceOrder_WhenOrderIsStored_TagsEveryLogLineWithTheRequestId()
    {
        var request = RequestTaggedWith("req-accepted");

        await Place(BodyOf(amount: 1_500), request);

        Assert.NotEmpty(_logger.Lines);
        Assert.All(_logger.Lines, line =>
            Assert.Equal("req-accepted", line.Scope[LogFields.CorrelationId]));
    }

    [Fact]
    public async Task PlaceOrder_WhenTheBodyIsRejected_TagsEveryLogLineWithTheRequestId()
    {
        var request = RequestTaggedWith("req-rejected");

        await Place(BodyOf(amount: 1_500, product: ""), request);

        Assert.NotEmpty(_logger.Lines);
        Assert.All(_logger.Lines, line =>
            Assert.Equal("req-rejected", line.Scope[LogFields.CorrelationId]));
    }

    [Fact]
    public async Task PlaceOrder_WhenTheStoreFails_TagsEveryLogLineWithTheRequestId()
    {
        _orderStore.PlaceAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
            .Returns(Result<Order>.Failure("The table is not reachable."));
        var request = RequestTaggedWith("req-failed");

        await Place(BodyOf(amount: 1_500), request);

        Assert.NotEmpty(_logger.Lines);
        Assert.All(_logger.Lines, line =>
            Assert.Equal("req-failed", line.Scope[LogFields.CorrelationId]));
    }

    [Fact]
    public async Task PlaceOrder_WhenTwoRequestsAreHandled_KeepsTheirCorrelationIdsApart()
    {
        await Place(BodyOf(amount: 1_500), RequestTaggedWith("req-first"));
        await Place(BodyOf(amount: 2_500), RequestTaggedWith("req-second"));

        var correlationIds = _logger.Lines
            .Select(line => line.Scope[LogFields.CorrelationId])
            .ToArray();

        Assert.Equal(["req-first", "req-second"], correlationIds);
    }

    [Theory]
    [InlineData(1_500, "")]
    [InlineData(1_500, "Laptop")]
    [InlineData(9_999, "Laptop")]
    public async Task PlaceOrder_WhateverTheOutcome_KeepsCustomerNameAndNotesOutOfTheLog(
        decimal amount, string product)
    {
        await Place(BodyOf(amount, product), RequestTaggedWith("req-pii"));

        Assert.NotEmpty(_logger.Lines);
        Assert.All(_logger.Lines, line =>
        {
            Assert.DoesNotContain(CustomerName, Rendered(line));
            Assert.DoesNotContain(Notes, Rendered(line));
        });
    }

    [Fact]
    public async Task PlaceOrder_WhenTheStoreFails_KeepsCustomerNameAndNotesOutOfTheLog()
    {
        _orderStore.PlaceAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
            .Returns(Result<Order>.Failure("The table is not reachable."));

        await Place(BodyOf(amount: 1_500), RequestTaggedWith("req-pii-failed"));

        Assert.NotEmpty(_logger.Lines);
        Assert.All(_logger.Lines, line =>
        {
            Assert.DoesNotContain(CustomerName, Rendered(line));
            Assert.DoesNotContain(Notes, Rendered(line));
        });
    }

    [Theory]
    [InlineData(1_500, "Laptop")]
    [InlineData(1_500, "")]
    public async Task PlaceOrder_WhateverTheOutcome_ReturnsTheCorrelationIdItWroteToTheLog(
        decimal amount, string product)
    {
        var result = await Place(BodyOf(amount, product), RequestTaggedWith("req-traceable"));

        Assert.Equal("req-traceable", HeaderOf(result, ApiHeaders.CorrelationId));
        Assert.NotEmpty(_logger.Lines);
        Assert.All(_logger.Lines, line =>
            Assert.Equal("req-traceable", line.Scope[LogFields.CorrelationId]));
    }

    [Fact]
    public async Task PlaceOrder_WhenTheStoreFails_ReturnsTheCorrelationIdItWroteToTheLog()
    {
        _orderStore.PlaceAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
            .Returns(Result<Order>.Failure("The table is not reachable."));

        var result = await Place(BodyOf(amount: 1_500), RequestTaggedWith("req-traceable"));

        Assert.Equal("req-traceable", HeaderOf(result, ApiHeaders.CorrelationId));
        Assert.NotEmpty(_logger.Lines);
        Assert.All(_logger.Lines, line =>
            Assert.Equal("req-traceable", line.Scope[LogFields.CorrelationId]));
    }

    private static string? HeaderOf(IHttpResult result, string header)
    {
        using var payload = result.Serialize(new HttpResultSerializationOptions
        {
            Format = HttpResultSerializationOptions.ProtocolFormat.HttpApi,
            Version = HttpResultSerializationOptions.ProtocolVersion.V2,
            Serializer = new CamelCaseLambdaJsonSerializer(),
        });

        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("headers").GetProperty(header).GetString();
    }

    private static string Rendered(LoggedLine line) =>
        line.Message + string.Join('|', line.Scope.Select(field => $"{field.Key}={field.Value}"));

    private static string BodyOf(decimal amount, string product = "Laptop") =>
        $$"""
        {"customerName":"{{CustomerName}}","product":"{{product}}","amount":{{amount}},"notes":"{{Notes}}"}
        """;

    private Task<IHttpResult> Place(string body, APIGatewayHttpApiV2ProxyRequest request)
    {
        var context = Substitute.For<ILambdaContext>();
        context.RemainingTime.Returns(TimeSpan.FromSeconds(30));
        return _sut.PlaceOrder(body, request, context);
    }

    private static APIGatewayHttpApiV2ProxyRequest RequestTaggedWith(string requestId) => new()
    {
        RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
        {
            RequestId = requestId,
        },
    };

    private void AcceptEveryOrder() =>
        _orderStore.PlaceAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
            .Returns(call => Result<Order>.Success(call.Arg<Order>()));
}
