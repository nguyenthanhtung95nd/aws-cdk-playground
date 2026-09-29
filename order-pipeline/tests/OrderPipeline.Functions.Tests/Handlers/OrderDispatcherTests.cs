using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using NSubstitute;
using OrderPipeline.Contracts;
using OrderPipeline.Domain.Common;
using OrderPipeline.Domain.Orders;
using OrderPipeline.Domain.Ports;
using OrderPipeline.Functions.Handlers;
using OrderPipeline.Functions.Tests.Fakes;

namespace OrderPipeline.Functions.Tests.Handlers;

public class OrderDispatcherTests
{
    private readonly IOrderStore _orderStore = Substitute.For<IOrderStore>();
    private readonly IOrderWorkQueue _workQueue = Substitute.For<IOrderWorkQueue>();
    private readonly RecordingLogger<OrderDispatcher> _logger = new();
    private readonly OrderDispatcher _sut;

    public OrderDispatcherTests()
    {
        _sut = new OrderDispatcher(_orderStore, _workQueue, _logger);
        StoreAnswers(OrderTransition.Moved);
        _workQueue.EnqueueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("message-1"));
    }

    [Fact]
    public async Task DispatchPlacedOrders_ForANewlyPlacedOrder_QueuesTheWorkForIt()
    {
        await _sut.DispatchPlacedOrders(EventOf(Inserted("order-1")), Context());

        await _workQueue.Received(1).EnqueueAsync("order-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchPlacedOrders_WhenTheSameEventIsDeliveredAgain_QueuesTheWorkAnyway()
    {
        StoreAnswers(OrderTransition.AlreadyMovedOn);

        await _sut.DispatchPlacedOrders(EventOf(Inserted("order-1")), Context());

        await _workQueue.Received(1).EnqueueAsync("order-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchPlacedOrders_WhenTheQueueRejects_AsksForOnlyThatRecordToBeRetried()
    {
        _workQueue.EnqueueAsync("order-2", Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure("the queue is gone"));

        var response = await _sut.DispatchPlacedOrders(
            EventOf(Inserted("order-1"), Inserted("order-2"), Inserted("order-3")), Context());

        var failure = Assert.Single(response.BatchItemFailures);
        Assert.Equal("event-order-2", failure.ItemIdentifier);
    }

    [Fact]
    public async Task DispatchPlacedOrders_WhenTheStoreFails_DoesNotQueueWorkForAnOrderItDoesNotOwn()
    {
        _orderStore.TransitionAsync(Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<OrderStatus>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderTransition>.Failure("the table is not reachable"));

        await _sut.DispatchPlacedOrders(EventOf(Inserted("order-1")), Context());

        await _workQueue.DidNotReceive().EnqueueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchPlacedOrders_ForANewlyPlacedOrder_StartsProcessingIt()
    {
        await _sut.DispatchPlacedOrders(EventOf(Inserted("order-1")), Context());

        await _orderStore.Received(1).TransitionAsync(
            "order-1", OrderStatus.Pending, OrderStatus.Processing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchPlacedOrders_WhenTheSameEventIsDeliveredAgain_ReportsNoFailure()
    {
        StoreAnswers(OrderTransition.AlreadyMovedOn);

        var response = await _sut.DispatchPlacedOrders(EventOf(Inserted("order-1")), Context());

        Assert.Empty(response.BatchItemFailures);
    }

    [Fact]
    public async Task DispatchPlacedOrders_WhenTheSameEventIsDeliveredAgain_DoesNotLogAnError()
    {
        StoreAnswers(OrderTransition.AlreadyMovedOn);

        await _sut.DispatchPlacedOrders(EventOf(Inserted("order-1")), Context());

        Assert.DoesNotContain(_logger.Lines, line => line.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task DispatchPlacedOrders_WhenTheStoreFails_AsksForOnlyThatRecordToBeRetried()
    {
        _orderStore.TransitionAsync("order-2", Arg.Any<OrderStatus>(), Arg.Any<OrderStatus>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderTransition>.Failure("the table is not reachable"));

        var response = await _sut.DispatchPlacedOrders(
            EventOf(Inserted("order-1"), Inserted("order-2"), Inserted("order-3")), Context());

        var failure = Assert.Single(response.BatchItemFailures);
        Assert.Equal("event-order-2", failure.ItemIdentifier);
    }

    [Fact]
    public async Task DispatchPlacedOrders_WhenTheStoreFails_LogsTheReasonAgainstTheOrder()
    {
        _orderStore.TransitionAsync(Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<OrderStatus>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderTransition>.Failure("the table is not reachable"));

        await _sut.DispatchPlacedOrders(EventOf(Inserted("order-1")), Context());

        Assert.Contains(_logger.Lines, line =>
            line.Level == LogLevel.Error && line.Message.Contains("order-1"));
    }

    [Theory]
    [InlineData("MODIFY")]
    [InlineData("REMOVE")]
    public async Task DispatchPlacedOrders_ForAnythingButAPlacement_LeavesTheOrderAlone(string eventName)
    {
        var record = Inserted("order-1");
        record.EventName = eventName;

        await _sut.DispatchPlacedOrders(EventOf(record), Context());

        await _orderStore.DidNotReceive().TransitionAsync(
            Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<OrderStatus>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchPlacedOrders_ForARecordWithoutAnOrderId_LeavesTheOrderAloneInsteadOfThrowing()
    {
        var record = Inserted("order-1");
        record.Dynamodb.NewImage.Clear();

        var response = await _sut.DispatchPlacedOrders(EventOf(record), Context());

        Assert.Empty(response.BatchItemFailures);
        await _orderStore.DidNotReceive().TransitionAsync(
            Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<OrderStatus>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchPlacedOrders_Always_TagsEveryLogLineWithTheOrderItIsWorkingOn()
    {
        await _sut.DispatchPlacedOrders(EventOf(Inserted("order-1")), Context());

        Assert.NotEmpty(_logger.Lines);
        Assert.All(_logger.Lines, line =>
        {
            Assert.Equal("order-1", line.Scope[LogFields.CorrelationId]);
            Assert.Equal("req-dispatch", line.Scope[LogFields.InvocationId]);
        });
    }

    // A whole batch used to share the invocation id as its correlation id, so looking up the one
    // order you cared about handed you the nine that happened to travel with it.
    [Fact]
    public async Task DispatchPlacedOrders_ForABatch_KeepsEachOrdersLinesApart()
    {
        await _sut.DispatchPlacedOrders(EventOf(Inserted("order-1"), Inserted("order-2")), Context());

        Assert.Equal(
            ["order-1", "order-2"],
            _logger.Lines.Select(line => line.Scope[LogFields.CorrelationId]).Distinct());
    }

    private void StoreAnswers(OrderTransition outcome) =>
        _orderStore.TransitionAsync(Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<OrderStatus>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderTransition>.Success(outcome));

    private static DynamoDBEvent.DynamodbStreamRecord Inserted(string orderId) => new()
    {
        EventID = $"event-{orderId}",
        EventName = "INSERT",
        Dynamodb = new DynamoDBEvent.StreamRecord
        {
            NewImage = new Dictionary<string, DynamoDBEvent.AttributeValue>
            {
                [OrderAttributes.OrderId] = new() { S = orderId }
            }
        }
    };

    private static DynamoDBEvent EventOf(params DynamoDBEvent.DynamodbStreamRecord[] records) =>
        new() { Records = [.. records] };

    private static ILambdaContext Context()
    {
        var context = Substitute.For<ILambdaContext>();
        context.AwsRequestId.Returns("req-dispatch");
        context.RemainingTime.Returns(TimeSpan.FromSeconds(30));
        return context;
    }
}
