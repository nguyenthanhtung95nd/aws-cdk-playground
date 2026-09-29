using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OrderPipeline.Contracts;
using OrderPipeline.Domain.Orders;
using OrderPipeline.Functions.Adapters;

namespace OrderPipeline.Functions.Tests.Adapters;

public class DynamoDbOrderStoreGetTests
{
    private const string TableName = "orders";
    private const string OrderId = "order-1";

    private readonly IAmazonDynamoDB _client = Substitute.For<IAmazonDynamoDB>();

    public DynamoDbOrderStoreGetTests() => TableHolds(Stored());

    [Fact]
    public async Task GetAsync_WhenTheOrderExists_ReturnsWhatTheWorkerNeedsToJudgeIt()
    {
        var result = await Get();

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderId, result.Value!.OrderId);
        Assert.Equal(1_500m, result.Value.Amount);
        Assert.Equal(OrderStatus.Processing, result.Value.Status);
    }

    [Fact]
    public async Task GetAsync_Always_ReadsConsistentlyBecauseTheOrderWasJustWritten()
    {
        await Get();

        var request = (GetItemRequest)_client.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IAmazonDynamoDB.GetItemAsync))
            .GetArguments()[0]!;

        Assert.True(request.ConsistentRead);
        Assert.Equal(OrderId, request.Key[OrderAttributes.OrderId].S);
    }

    [Fact]
    public async Task GetAsync_WhenTheOrderIsNotThere_SaysSoInsteadOfReturningNothing()
    {
        _client.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse());

        var result = await Get();

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task GetAsync_WhenTheRowCannotBeRead_SaysSoInsteadOfThrowing()
    {
        TableHolds(new Dictionary<string, AttributeValue>
        {
            [OrderAttributes.OrderId] = new() { S = OrderId }
        });

        var result = await Get();

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetAsync_WhenTheInvocationRunsOutOfTime_SaysSoInsteadOfThrowing()
    {
        _client.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<GetItemResponse>>(_ => throw new OperationCanceledException());

        var result = await Get();

        Assert.False(result.IsSuccess);
        Assert.Contains("ran out of time", result.Error);
    }

    private void TableHolds(Dictionary<string, AttributeValue> item) =>
        _client.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = item });

    private Task<Domain.Common.Result<Order>> Get() =>
        new DynamoDbOrderStore(_client, TableName, NullLogger<DynamoDbOrderStore>.Instance)
            .GetAsync(OrderId, CancellationToken.None);

    private static Dictionary<string, AttributeValue> Stored() => new()
    {
        [OrderAttributes.OrderId] = new() { S = OrderId },
        [OrderAttributes.CustomerName] = new() { S = "Ada" },
        [OrderAttributes.Product] = new() { S = "Keyboard" },
        [OrderAttributes.Amount] = new() { N = "1500" },
        [OrderAttributes.Notes] = new() { S = string.Empty },
        [OrderAttributes.Status] = new() { S = "PROCESSING" },
        [OrderAttributes.ValueTier] = new() { S = ValueTiers.Standard },
        [OrderAttributes.PlacedAt] = new() { S = "2026-09-24T08:30:00.0000000+00:00" }
    };
}
