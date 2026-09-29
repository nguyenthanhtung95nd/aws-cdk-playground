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

public class OrderWorkerTests
{
    private const decimal BusinessLimit = 50_000m;
    private const decimal WithinLimit = 900m;

    private readonly IOrderStore _orderStore = Substitute.For<IOrderStore>();
    private readonly OrderThresholds _thresholds = new(10_000m, BusinessLimit);
    private readonly RecordingLogger<OrderWorker> _logger = new();
    private readonly OrderWorker _sut;

    public OrderWorkerTests()
    {
        _sut = new OrderWorker(_orderStore, _thresholds, _logger);
        StoreAnswers(OrderTransition.Moved);
        FailAnswers(OrderTransition.Moved);
        StoreHolds(WithinLimit);
    }

    [Fact]
    public async Task CompleteOrders_WhenTheOrderIsOverTheBusinessLimit_SettlesItAsFailedWithTheReason()
    {
        StoreHolds(BusinessLimit + 0.01m);

        await _sut.CompleteOrders(EventOf(Work("order-1")), Context());

        await _orderStore.Received(1).FailAsync(
            "order-1",
            OrderStatus.Processing,
            Arg.Is<OrderFailure>(failure =>
                failure.Kind == FailureKind.Business && failure.Reason.Contains("business limit")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteOrders_WhenTheOrderIsOverTheBusinessLimit_NeverCompletesIt()
    {
        StoreHolds(BusinessLimit + 0.01m);

        await _sut.CompleteOrders(EventOf(Work("order-1")), Context());

        await _orderStore.DidNotReceive().TransitionAsync(
            Arg.Any<string>(), Arg.Any<OrderStatus>(), OrderStatus.Completed, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteOrders_WhenTheOrderIsOverTheBusinessLimit_DoesNotAskForTheMessageBack()
    {
        StoreHolds(BusinessLimit + 0.01m);

        var response = await _sut.CompleteOrders(EventOf(Work("order-1")), Context());

        Assert.Empty(response.BatchItemFailures);
    }

    [Fact]
    public async Task CompleteOrders_WhenTheOrderIsOverTheBusinessLimit_DoesNotLogAnError()
    {
        StoreHolds(BusinessLimit + 0.01m);

        await _sut.CompleteOrders(EventOf(Work("order-1")), Context());

        Assert.DoesNotContain(_logger.Lines, line => line.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task CompleteOrders_WhenTheOrderSitsExactlyOnTheBusinessLimit_CompletesIt()
    {
        StoreHolds(BusinessLimit);

        await _sut.CompleteOrders(EventOf(Work("order-1")), Context());

        await _orderStore.Received(1).TransitionAsync(
            "order-1", OrderStatus.Processing, OrderStatus.Completed, Arg.Any<CancellationToken>());
        await _orderStore.DidNotReceive().FailAsync(
            Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<OrderFailure>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteOrders_WhenTheOrderAsksToFailOnPurpose_AsksForTheMessageBack()
    {
        StoreHoldsNotes(FailureProbe.Marker);

        var response = await _sut.CompleteOrders(EventOf(Work("order-1")), Context());

        Assert.Single(response.BatchItemFailures);
    }

    [Fact]
    public async Task CompleteOrders_WhenTheOrderAsksToFailOnPurpose_LeavesItWhereItIs()
    {
        StoreHoldsNotes(FailureProbe.Marker);

        await _sut.CompleteOrders(EventOf(Work("order-1")), Context());

        await _orderStore.DidNotReceive().TransitionAsync(
            Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<OrderStatus>(), Arg.Any<CancellationToken>());
        await _orderStore.DidNotReceive().FailAsync(
            Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<OrderFailure>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteOrders_WhenAnOrderIsBothOverTheLimitAndAskingToFail_RejectsItOutright()
    {
        StoreHoldsNotes(FailureProbe.Marker, BusinessLimit + 0.01m);

        var response = await _sut.CompleteOrders(EventOf(Work("order-1")), Context());

        Assert.Empty(response.BatchItemFailures);
        await _orderStore.Received(1).FailAsync(
            Arg.Any<string>(),
            Arg.Any<OrderStatus>(),
            Arg.Is<OrderFailure>(failure => failure.Kind == FailureKind.Business),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteOrders_WhenTheRejectionCannotBeWritten_AsksForTheMessageBack()
    {
        StoreHolds(BusinessLimit + 0.01m);
        _orderStore.FailAsync(Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<OrderFailure>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderTransition>.Failure("the table is not reachable"));

        var response = await _sut.CompleteOrders(EventOf(Work("order-1")), Context());

        Assert.Single(response.BatchItemFailures);
    }

    [Fact]
    public async Task CompleteOrders_WhenTheOrderCannotBeRead_AsksForTheMessageBackInsteadOfSettlingIt()
    {
        _orderStore.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<Order>.Failure("the table is not reachable"));

        var response = await _sut.CompleteOrders(EventOf(Work("order-1")), Context());

        Assert.Single(response.BatchItemFailures);
        await _orderStore.DidNotReceive().TransitionAsync(
            Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<OrderStatus>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteOrders_ForAQueuedOrder_CompletesIt()
    {
        await _sut.CompleteOrders(EventOf(Work("order-1")), Context());

        await _orderStore.Received(1).TransitionAsync(
            "order-1", OrderStatus.Processing, OrderStatus.Completed, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteOrders_WhenEveryOrderCompletes_LeavesTheQueueEmpty()
    {
        var response = await _sut.CompleteOrders(
            EventOf(Work("order-1"), Work("order-2"), Work("order-3")), Context());

        Assert.Empty(response.BatchItemFailures);
    }

    [Fact]
    public async Task CompleteOrders_WhenTheSameMessageArrivesAgain_ReportsNoFailure()
    {
        StoreAnswers(OrderTransition.AlreadyMovedOn);

        var response = await _sut.CompleteOrders(EventOf(Work("order-1")), Context());

        Assert.Empty(response.BatchItemFailures);
    }

    [Fact]
    public async Task CompleteOrders_WhenTheSameMessageArrivesAgain_DoesNotLogAnError()
    {
        StoreAnswers(OrderTransition.AlreadyMovedOn);

        await _sut.CompleteOrders(EventOf(Work("order-1")), Context());

        Assert.DoesNotContain(_logger.Lines, line => line.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task CompleteOrders_WhenOneMessageInABatchOfTenFails_AsksForOnlyThatMessageBack()
    {
        var batch = Enumerable.Range(1, 10).Select(number => Work($"order-{number}")).ToArray();

        _orderStore.TransitionAsync("order-7", Arg.Any<OrderStatus>(), Arg.Any<OrderStatus>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderTransition>.Failure("the table is not reachable"));

        var response = await _sut.CompleteOrders(EventOf(batch), Context());

        var failure = Assert.Single(response.BatchItemFailures);
        Assert.Equal("message-order-7", failure.ItemIdentifier);
    }

    [Fact]
    public async Task CompleteOrders_WhenOneMessageInABatchOfTenFails_StillCompletesTheOtherNine()
    {
        var batch = Enumerable.Range(1, 10).Select(number => Work($"order-{number}")).ToArray();

        _orderStore.TransitionAsync("order-7", Arg.Any<OrderStatus>(), Arg.Any<OrderStatus>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderTransition>.Failure("the table is not reachable"));

        await _sut.CompleteOrders(EventOf(batch), Context());

        await _orderStore.Received(10).TransitionAsync(
            Arg.Any<string>(), OrderStatus.Processing, OrderStatus.Completed, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteOrders_WhenTheStoreFails_LogsTheReasonAgainstTheOrder()
    {
        _orderStore.TransitionAsync(Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<OrderStatus>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderTransition>.Failure("the table is not reachable"));

        await _sut.CompleteOrders(EventOf(Work("order-1")), Context());

        Assert.Contains(_logger.Lines, line =>
            line.Level == LogLevel.Error && line.Message.Contains("order-1"));
    }

    [Fact]
    public async Task CompleteOrders_ForAMessageWithoutAnOrderId_AsksForItBackInsteadOfTouchingTheStore()
    {
        var response = await _sut.CompleteOrders(EventOf(Work(string.Empty)), Context());

        Assert.Single(response.BatchItemFailures);
        await _orderStore.DidNotReceive().TransitionAsync(
            Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<OrderStatus>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteOrders_Always_TagsEveryLogLineWithTheOrderItIsWorkingOn()
    {
        await _sut.CompleteOrders(EventOf(Work("order-1")), Context());

        Assert.NotEmpty(_logger.Lines);
        Assert.All(_logger.Lines, line =>
        {
            Assert.Equal("order-1", line.Scope[LogFields.CorrelationId]);
            Assert.Equal("req-worker", line.Scope[LogFields.InvocationId]);
        });
    }

    // There is no order to correlate a message like this to, so it is tagged with the run it
    // arrived in and nothing else rather than being filed under an order id it does not have.
    [Fact]
    public async Task CompleteOrders_ForAMessageWithoutAnOrderId_TagsTheLineWithTheRunOnly()
    {
        await _sut.CompleteOrders(EventOf(Work(string.Empty)), Context());

        var line = Assert.Single(_logger.Lines);
        Assert.Equal("req-worker", line.Scope[LogFields.InvocationId]);
        Assert.False(line.Scope.ContainsKey(LogFields.CorrelationId));
    }

    // A whole batch used to share the invocation id as its correlation id, so looking up the one
    // order you cared about handed you the nine that happened to travel with it.
    [Fact]
    public async Task CompleteOrders_ForABatch_KeepsEachOrdersLinesApart()
    {
        await _sut.CompleteOrders(EventOf(Work("order-1"), Work("order-2")), Context());

        Assert.Equal(
            ["order-1", "order-2"],
            _logger.Lines.Select(line => line.Scope[LogFields.CorrelationId]).Distinct());
    }

    private void StoreHolds(decimal amount) => StoreHoldsNotes(string.Empty, amount);

    private void StoreHoldsNotes(string notes, decimal amount = WithinLimit) =>
        _orderStore.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Result<Order>.Success(new Order
            {
                OrderId = call.Arg<string>(),
                CustomerName = "Ada",
                Product = "Keyboard",
                Amount = amount,
                Notes = notes,
                Status = OrderStatus.Processing,
                IsHighValue = false,
                PlacedAt = DateTimeOffset.UnixEpoch
            }));

    private void StoreAnswers(OrderTransition outcome) =>
        _orderStore.TransitionAsync(Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<OrderStatus>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderTransition>.Success(outcome));

    private void FailAnswers(OrderTransition outcome) =>
        _orderStore.FailAsync(Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<OrderFailure>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderTransition>.Success(outcome));

    private static SQSEvent.SQSMessage Work(string orderId) =>
        new() { MessageId = $"message-{orderId}", Body = orderId };

    private static SQSEvent EventOf(params SQSEvent.SQSMessage[] messages) =>
        new() { Records = [.. messages] };

    private static ILambdaContext Context()
    {
        var context = Substitute.For<ILambdaContext>();
        context.AwsRequestId.Returns("req-worker");
        context.RemainingTime.Returns(TimeSpan.FromSeconds(30));
        return context;
    }
}
