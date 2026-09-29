using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using NSubstitute;
using OrderPipeline.Contracts;
using OrderPipeline.Domain.Common;
using OrderPipeline.Domain.Orders;
using OrderPipeline.Domain.Ports;
using OrderPipeline.Functions.Handlers;
using OrderPipeline.Functions.Tests.Fakes;

namespace OrderPipeline.Functions.Tests.Handlers;

public class OrderFailureSettlerTests
{
    private readonly IOrderStore _orderStore = Substitute.For<IOrderStore>();
    private readonly RecordingLogger<OrderFailureSettler> _logger = new();
    private readonly OrderFailureSettler _sut;

    public OrderFailureSettlerTests()
    {
        _sut = new OrderFailureSettler(_orderStore, _logger);
        StoreAnswers(OrderTransition.Moved);
    }

    [Fact]
    public async Task SettleFailures_ForAParkedOrder_SettlesItAsATechnicalFailure()
    {
        await _sut.SettleFailures(EventOf(Parked("order-1")), Context());

        await _orderStore.Received(1).FailAsync(
            "order-1",
            OrderStatus.Processing,
            Arg.Is<OrderFailure>(failure => failure.Kind == FailureKind.Technical),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SettleFailures_ForAParkedOrder_ExplainsThatTheRetryBudgetRanOut()
    {
        await _sut.SettleFailures(EventOf(Parked("order-1")), Context());

        await _orderStore.Received(1).FailAsync(
            Arg.Any<string>(),
            Arg.Any<OrderStatus>(),
            Arg.Is<OrderFailure>(failure => failure.Reason.Contains("retry budget")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SettleFailures_ForAParkedOrder_NeverReadsTheOrderItSettles()
    {
        await _sut.SettleFailures(EventOf(Parked("order-1")), Context());

        await _orderStore.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SettleFailures_WhenTheOrderAlreadySettledItself_ReportsNoFailure()
    {
        StoreAnswers(OrderTransition.AlreadyMovedOn);

        var response = await _sut.SettleFailures(EventOf(Parked("order-1")), Context());

        Assert.Empty(response.BatchItemFailures);
    }

    [Fact]
    public async Task SettleFailures_WhenTheOrderAlreadySettledItself_DoesNotLogAnError()
    {
        StoreAnswers(OrderTransition.AlreadyMovedOn);

        await _sut.SettleFailures(EventOf(Parked("order-1")), Context());

        Assert.DoesNotContain(_logger.Lines, line => line.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task SettleFailures_WhenTheWriteFails_AsksForOnlyThatMessageBack()
    {
        _orderStore.FailAsync("order-2", Arg.Any<OrderStatus>(), Arg.Any<OrderFailure>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderTransition>.Failure("the table is not reachable"));

        var response = await _sut.SettleFailures(
            EventOf(Parked("order-1"), Parked("order-2"), Parked("order-3")), Context());

        var failure = Assert.Single(response.BatchItemFailures);
        Assert.Equal("parked-order-2", failure.ItemIdentifier);
    }

    [Fact]
    public async Task SettleFailures_ForAMessageWithoutAnOrderId_AsksForItBackInsteadOfTouchingTheStore()
    {
        var response = await _sut.SettleFailures(EventOf(Parked(string.Empty)), Context());

        Assert.Single(response.BatchItemFailures);
        await _orderStore.DidNotReceive().FailAsync(
            Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<OrderFailure>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SettleFailures_Always_TagsEveryLogLineWithTheOrderItIsSettling()
    {
        await _sut.SettleFailures(EventOf(Parked("order-1")), Context());

        Assert.NotEmpty(_logger.Lines);
        Assert.All(_logger.Lines, line =>
        {
            Assert.Equal("order-1", line.Scope[LogFields.CorrelationId]);
            Assert.Equal("req-settler", line.Scope[LogFields.InvocationId]);
        });
    }

    private void StoreAnswers(OrderTransition outcome) =>
        _orderStore.FailAsync(Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<OrderFailure>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderTransition>.Success(outcome));

    private static SQSEvent.SQSMessage Parked(string orderId) =>
        new() { MessageId = $"parked-{orderId}", Body = orderId };

    private static SQSEvent EventOf(params SQSEvent.SQSMessage[] messages) =>
        new() { Records = [.. messages] };

    private static ILambdaContext Context()
    {
        var context = Substitute.For<ILambdaContext>();
        context.AwsRequestId.Returns("req-settler");
        context.RemainingTime.Returns(TimeSpan.FromSeconds(30));
        return context;
    }
}
