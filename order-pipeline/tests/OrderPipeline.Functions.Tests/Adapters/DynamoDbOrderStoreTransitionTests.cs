using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OrderPipeline.Contracts;
using OrderPipeline.Domain.Orders;
using OrderPipeline.Functions.Adapters;

namespace OrderPipeline.Functions.Tests.Adapters;

public class DynamoDbOrderStoreTransitionTests
{
    private const string TableName = "orders";
    private const string OrderId = "order-1";

    private readonly IAmazonDynamoDB _client = Substitute.For<IAmazonDynamoDB>();

    [Fact]
    public async Task TransitionAsync_WhenTheOrderIsStillInTheExpectedState_ReportsItMoved()
    {
        var result = await Transition(OrderStatus.Pending, OrderStatus.Processing);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderTransition.Moved, result.Value);
    }

    [Fact]
    public async Task TransitionAsync_WhenTheSameEventIsDeliveredAgain_IsNotAFailure()
    {
        _client.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<UpdateItemResponse>>(_ => throw new ConditionalCheckFailedException("already moved"));

        var result = await Transition(OrderStatus.Pending, OrderStatus.Processing);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderTransition.AlreadyMovedOn, result.Value);
    }

    [Fact]
    public async Task TransitionAsync_Always_WritesOnlyWhileTheOrderIsStillInTheStateItReadFrom()
    {
        var request = await CaptureUpdate(OrderStatus.Pending, OrderStatus.Processing);

        Assert.Contains("#status = :from", request.ConditionExpression);
        Assert.Equal("PENDING", request.ExpressionAttributeValues[":from"].S);
        Assert.Equal("PROCESSING", request.ExpressionAttributeValues[":to"].S);
        Assert.Equal(OrderAttributes.Status, request.ExpressionAttributeNames["#status"]);
    }

    [Fact]
    public async Task TransitionAsync_Always_RefusesToResurrectAnOrderThatWasNeverThere()
    {
        var request = await CaptureUpdate(OrderStatus.Pending, OrderStatus.Processing);

        Assert.Contains("attribute_exists(#id)", request.ConditionExpression);
    }

    [Theory]
    [InlineData(OrderStatus.Completed, OrderStatus.Processing)]
    [InlineData(OrderStatus.Failed, OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing, OrderStatus.Pending)]
    [InlineData(OrderStatus.Pending, OrderStatus.Completed)]
    public async Task TransitionAsync_ForAMoveTheLifecycleForbids_FailsWithoutTouchingTheTable(
        OrderStatus from, OrderStatus to)
    {
        var result = await Transition(from, to);

        Assert.False(result.IsSuccess);
        await _client.DidNotReceive().UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TransitionAsync_WhenTheInvocationRunsOutOfTime_ReportsFailureInsteadOfThrowing()
    {
        _client.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<UpdateItemResponse>>(_ => throw new OperationCanceledException());

        var result = await Transition(OrderStatus.Pending, OrderStatus.Processing);

        Assert.False(result.IsSuccess);
        Assert.Contains("ran out of time", result.Error);
    }

    [Fact]
    public async Task TransitionAsync_WhenTheTableIsUnreachable_ReportsFailureInsteadOfThrowing()
    {
        _client.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<UpdateItemResponse>>(_ => throw new AmazonDynamoDBException("table is gone"));

        var result = await Transition(OrderStatus.Pending, OrderStatus.Processing);

        Assert.False(result.IsSuccess);
        Assert.Contains("table is gone", result.Error);
    }

    private Task<OrderPipeline.Domain.Common.Result<OrderTransition>> Transition(OrderStatus from, OrderStatus to) =>
        new DynamoDbOrderStore(_client, TableName, NullLogger<DynamoDbOrderStore>.Instance)
            .TransitionAsync(OrderId, from, to, CancellationToken.None);

    private async Task<UpdateItemRequest> CaptureUpdate(OrderStatus from, OrderStatus to)
    {
        await Transition(from, to);

        return (UpdateItemRequest)_client.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IAmazonDynamoDB.UpdateItemAsync))
            .GetArguments()[0]!;
    }
}
