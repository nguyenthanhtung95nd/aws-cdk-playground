using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OrderPipeline.Domain.Orders;
using OrderPipeline.Contracts;
using OrderPipeline.Domain.Ports;
using OrderPipeline.Functions.Adapters;
using OrderPipeline.Functions.Tests.Fakes;

namespace OrderPipeline.Functions.Tests.Adapters;

public class DynamoDbOrderStoreListTests
{
    private const string TableName = "orders";

    private readonly IAmazonDynamoDB _client = Substitute.For<IAmazonDynamoDB>();

    private DynamoDbOrderStore Store() => new(_client, TableName, NullLogger<DynamoDbOrderStore>.Instance);

    private static Dictionary<string, AttributeValue> Item(
        string orderId,
        string valueTier,
        string status = "PENDING",
        string placedAt = "2026-09-24T08:00:00.0000000+00:00") => new()
    {
        ["orderId"] = new AttributeValue { S = orderId },
        ["customerName"] = new AttributeValue { S = "Alice" },
        ["product"] = new AttributeValue { S = "Headphones" },
        ["amount"] = new AttributeValue { N = "1499" },
        ["notes"] = new AttributeValue { S = string.Empty },
        ["status"] = new AttributeValue { S = status },
        ["valueTier"] = new AttributeValue { S = valueTier },
        ["placedAt"] = new AttributeValue { S = placedAt }
    };

    private void RespondWith(QueryResponse answer) =>
        _client.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(answer));

    private QueryRequest CapturedQuery() =>
        (QueryRequest)_client.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IAmazonDynamoDB.QueryAsync))
            .GetArguments()[0]!;

    [Fact]
    public async Task ListAsync_ForAHighValueMarker_ReadsBackAsHighValue()
    {
        RespondWith(new QueryResponse { Items = [Item("a", ValueTiers.HighValue)] });

        var page = await Store().ListAsync(OrderPageRequest.First, CancellationToken.None);

        Assert.True(page.Value!.Orders.Single().IsHighValue);
    }

    [Fact]
    public async Task ListAsync_ForAStandardMarker_ReadsBackAsOrdinary()
    {
        RespondWith(new QueryResponse { Items = [Item("a", ValueTiers.Standard)] });

        var page = await Store().ListAsync(OrderPageRequest.First, CancellationToken.None);

        Assert.False(page.Value!.Orders.Single().IsHighValue);
    }

    [Fact]
    public async Task ListAsync_Always_ReadsTheStatusBackAsTheEnum()
    {
        RespondWith(new QueryResponse { Items = [Item("a", ValueTiers.Standard, status: "COMPLETED")] });

        var page = await Store().ListAsync(OrderPageRequest.First, CancellationToken.None);

        Assert.Equal(OrderStatus.Completed, page.Value!.Orders.Single().Status);
    }

    // Ordering used to be this adapter's job, done by reading the whole table and sorting what came
    // back. It is the index's job now, so what is worth asserting is the request, not the answer.
    [Fact]
    public async Task ListAsync_Always_AsksTheRecencyIndexForTheNewestFirst()
    {
        RespondWith(new QueryResponse { Items = [] });

        await Store().ListAsync(OrderPageRequest.First, CancellationToken.None);

        var query = CapturedQuery();
        Assert.Equal(OrderIndexes.ByRecency, query.IndexName);
        Assert.False(query.ScanIndexForward);
        Assert.Equal(RecencyBuckets.All, query.ExpressionAttributeValues[":bucket"].S);
    }

    [Fact]
    public async Task ListAsync_Always_AsksForOnlyThePageItWasAskedFor()
    {
        RespondWith(new QueryResponse { Items = [] });
        var asked = OrderPageRequest.For(10, null).Value;

        await Store().ListAsync(asked, CancellationToken.None);

        Assert.Equal(10, CapturedQuery().Limit);
    }

    // The old reader looped until the table ran out. One page now means one call, whatever else
    // the table holds, because that is the entire point of asking for a page.
    [Fact]
    public async Task ListAsync_WhenMoreOrdersExist_ReadsOnePageAndSaysWhereToResume()
    {
        RespondWith(new QueryResponse
        {
            Items = [Item("a", ValueTiers.Standard)],
            LastEvaluatedKey = new Dictionary<string, AttributeValue>
            {
                [OrderAttributes.OrderId] = new() { S = "a" },
                [OrderAttributes.RecencyBucket] = new() { S = RecencyBuckets.All },
                [OrderAttributes.RecencyCursor] = new() { S = "2026-09-24T08:00:00.0000000Z#a" }
            }
        });

        var page = await Store().ListAsync(OrderPageRequest.First, CancellationToken.None);

        await _client.Received(1).QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>());
        Assert.NotNull(page.Value!.ResumeFrom);
    }

    [Fact]
    public async Task ListAsync_WhenThereIsNothingMore_OffersNowhereToResumeFrom()
    {
        RespondWith(new QueryResponse { Items = [Item("a", ValueTiers.Standard)] });

        var page = await Store().ListAsync(OrderPageRequest.First, CancellationToken.None);

        Assert.Null(page.Value!.ResumeFrom);
    }

    [Fact]
    public async Task ListAsync_WithACursor_ResumesFromWhereTheLastPageStopped()
    {
        RespondWith(new QueryResponse
        {
            Items = [],
            LastEvaluatedKey = new Dictionary<string, AttributeValue>
            {
                [OrderAttributes.OrderId] = new() { S = "a" },
                [OrderAttributes.RecencyBucket] = new() { S = RecencyBuckets.All },
                [OrderAttributes.RecencyCursor] = new() { S = "2026-09-24T08:00:00.0000000Z#a" }
            }
        });
        var first = await Store().ListAsync(OrderPageRequest.First, CancellationToken.None);
        _client.ClearReceivedCalls();

        await Store().ListAsync(
            OrderPageRequest.For(null, first.Value!.ResumeFrom).Value, CancellationToken.None);

        var resumed = CapturedQuery().ExclusiveStartKey;
        Assert.Equal("a", resumed[OrderAttributes.OrderId].S);
        Assert.Equal("2026-09-24T08:00:00.0000000Z#a", resumed[OrderAttributes.RecencyCursor].S);
    }

    // The cursor travels through a query string, so it comes back as a stranger's text.
    [Theory]
    [InlineData("not base64 at all")]
    [InlineData("eyJvcmRlcklkIjoiYSJ9")]
    public async Task ListAsync_WithACursorThisApiNeverHandedOut_ReportsFailureInsteadOfThrowing(string cursor)
    {
        var page = await Store().ListAsync(
            OrderPageRequest.For(null, cursor).Value, CancellationToken.None);

        Assert.False(page.IsSuccess);
        Assert.Contains("cursor", page.Error);
        await _client.DidNotReceive().QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>());
    }
}

public class DynamoDbOrderStoreResilienceTests
{
    private const string TableName = "orders";

    private readonly IAmazonDynamoDB _client = Substitute.For<IAmazonDynamoDB>();
    private readonly RecordingLogger<DynamoDbOrderStore> _logger = new();

    private DynamoDbOrderStore Store() => new(_client, TableName, _logger);

    private static Dictionary<string, AttributeValue> Complete(string orderId) => new()
    {
        [OrderAttributes.OrderId] = new AttributeValue { S = orderId },
        [OrderAttributes.CustomerName] = new AttributeValue { S = "Alice" },
        [OrderAttributes.Product] = new AttributeValue { S = "Headphones" },
        [OrderAttributes.Amount] = new AttributeValue { N = "1499" },
        [OrderAttributes.Notes] = new AttributeValue { S = string.Empty },
        [OrderAttributes.Status] = new AttributeValue { S = "PENDING" },
        [OrderAttributes.ValueTier] = new AttributeValue { S = ValueTiers.Standard },
        [OrderAttributes.PlacedAt] = new AttributeValue { S = "2026-09-24T08:00:00.0000000+00:00" }
    };

    private async Task<IReadOnlyList<Order>> ListWith(params Dictionary<string, AttributeValue>[] items)
    {
        _client.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new QueryResponse { Items = [.. items] }));

        var listed = await Store().ListAsync(OrderPageRequest.First, CancellationToken.None);

        Assert.True(listed.IsSuccess);
        return listed.Value!.Orders;
    }

    [Theory]
    [InlineData(OrderAttributes.OrderId)]
    [InlineData(OrderAttributes.Status)]
    [InlineData(OrderAttributes.Amount)]
    [InlineData(OrderAttributes.PlacedAt)]
    public async Task ListAsync_WhenAnItemLacksSomethingThatMakesItAnOrder_SkipsOnlyThatItem(string missing)
    {
        var broken = Complete("broken");
        broken.Remove(missing);

        var orders = await ListWith(Complete("good-1"), broken, Complete("good-2"));

        Assert.Equal(2, orders.Count);
        Assert.DoesNotContain(orders, order => order.OrderId == "broken");
    }

    [Fact]
    public async Task ListAsync_WhenAnItemCannotBeRead_SaysWhichRowItSkipped()
    {
        var broken = Complete("broken");
        broken.Remove(OrderAttributes.PlacedAt);

        await ListWith(Complete("good-1"), broken);

        var warning = Assert.Single(_logger.Lines, line => line.Level == LogLevel.Warning);
        Assert.Contains("broken", warning.Message);
    }

    [Fact]
    public async Task ListAsync_WhenEveryItemIsReadable_SaysNothing()
    {
        await ListWith(Complete("good-1"), Complete("good-2"));

        Assert.Empty(_logger.Lines);
    }

    [Fact]
    public async Task ListAsync_WhenAnItemHasAnUnknownStatus_SkipsOnlyThatItem()
    {
        var broken = Complete("broken");
        broken[OrderAttributes.Status] = new AttributeValue { S = "SHIPPED" };

        var orders = await ListWith(Complete("good-1"), broken);

        Assert.Single(orders);
    }

    [Fact]
    public async Task ListAsync_WhenAnItemLacksTheValueTier_StillReturnsItAsOrdinary()
    {
        var withoutTier = Complete("no-tier");
        withoutTier.Remove(OrderAttributes.ValueTier);

        var orders = await ListWith(Complete("good-1"), withoutTier);

        Assert.Equal(2, orders.Count);
        Assert.False(orders.Single(order => order.OrderId == "no-tier").IsHighValue);
    }

    [Fact]
    public async Task ListAsync_WhenAnItemLacksOptionalText_UsesEmptyRatherThanFailing()
    {
        var sparse = Complete("sparse");
        sparse.Remove(OrderAttributes.CustomerName);
        sparse.Remove(OrderAttributes.Notes);

        var orders = await ListWith(sparse);

        Assert.Equal(string.Empty, orders.Single().CustomerName);
        Assert.Equal(string.Empty, orders.Single().Notes);
    }

    [Fact]
    public async Task ListAsync_WhenTheTableIsUnreachable_ReportsFailureInsteadOfThrowing()
    {
        _client.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<QueryResponse>>(_ => throw new AmazonDynamoDBException("table is gone"));

        var listed = await Store().ListAsync(OrderPageRequest.First, CancellationToken.None);

        Assert.False(listed.IsSuccess);
        Assert.Contains("table is gone", listed.Error);
    }

    [Fact]
    public async Task ListAsync_WhenTheInvocationRunsOutOfTime_ReportsFailureInsteadOfThrowing()
    {
        _client.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<QueryResponse>>(_ => throw new OperationCanceledException());

        var listed = await Store().ListAsync(OrderPageRequest.First, Expired);

        Assert.False(listed.IsSuccess);
        Assert.Contains("ran out of time", listed.Error);
    }

    private static CancellationToken Expired
    {
        get
        {
            using var source = new CancellationTokenSource();
            source.Cancel();
            return source.Token;
        }
    }
}
