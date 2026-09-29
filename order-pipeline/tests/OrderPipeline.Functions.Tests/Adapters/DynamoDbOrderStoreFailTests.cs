using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OrderPipeline.Contracts;
using OrderPipeline.Domain.Orders;
using OrderPipeline.Functions.Adapters;

namespace OrderPipeline.Functions.Tests.Adapters;

public class DynamoDbOrderStoreFailTests
{
    private const string TableName = "orders";
    private const string OrderId = "order-1";
    private static readonly OrderFailure Failure =
        new(FailureKind.Business, "The order is over the business limit.");

    private readonly IAmazonDynamoDB _client = Substitute.For<IAmazonDynamoDB>();

    [Fact]
    public async Task FailAsync_WhenTheOrderIsStillProcessing_ReportsItMoved()
    {
        var result = await Fail();

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderTransition.Moved, result.Value);
    }

    [Fact]
    public async Task FailAsync_Always_RecordsTheReasonAlongsideTheStatus()
    {
        var request = await CaptureUpdate();

        Assert.Contains("#status = :to", request.UpdateExpression);
        Assert.Contains("#reason = :reason", request.UpdateExpression);
        Assert.Equal(OrderAttributes.FailureReason, request.ExpressionAttributeNames["#reason"]);
        Assert.Equal(Failure.Reason, request.ExpressionAttributeValues[":reason"].S);
        Assert.Equal(OrderAttributes.FailureKind, request.ExpressionAttributeNames["#kind"]);
        Assert.Equal("BUSINESS", request.ExpressionAttributeValues[":kind"].S);
        Assert.Equal("FAILED", request.ExpressionAttributeValues[":to"].S);
    }

    [Fact]
    public async Task FailAsync_Always_WritesOnlyWhileTheOrderIsStillInTheStateItReadFrom()
    {
        var request = await CaptureUpdate();

        Assert.Contains("attribute_exists(#id)", request.ConditionExpression);
        Assert.Contains("#status = :from", request.ConditionExpression);
        Assert.Equal("PROCESSING", request.ExpressionAttributeValues[":from"].S);
    }

    [Fact]
    public async Task FailAsync_WhenTheOrderWasAlreadySettled_IsNotAFailure()
    {
        _client.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<UpdateItemResponse>>(_ => throw new ConditionalCheckFailedException("already settled"));

        var result = await Fail();

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderTransition.AlreadyMovedOn, result.Value);
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Completed)]
    public async Task FailAsync_FromAStateThatCannotFail_RefusesWithoutTouchingTheTable(OrderStatus from)
    {
        var result = await Fail(from);

        Assert.False(result.IsSuccess);
        await _client.DidNotReceive().UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailAsync_WhenTheInvocationRunsOutOfTime_SaysSoInsteadOfThrowing()
    {
        _client.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<UpdateItemResponse>>(_ => throw new OperationCanceledException());

        var result = await Fail();

        Assert.False(result.IsSuccess);
        Assert.Contains("ran out of time", result.Error);
    }

    private Task<Domain.Common.Result<OrderTransition>> Fail(OrderStatus from = OrderStatus.Processing) =>
        new DynamoDbOrderStore(_client, TableName, NullLogger<DynamoDbOrderStore>.Instance)
            .FailAsync(OrderId, from, Failure, CancellationToken.None);

    private async Task<UpdateItemRequest> CaptureUpdate()
    {
        await Fail();

        return (UpdateItemRequest)_client.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IAmazonDynamoDB.UpdateItemAsync))
            .GetArguments()[0]!;
    }
}
