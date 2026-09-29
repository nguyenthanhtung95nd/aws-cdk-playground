using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OrderPipeline.Domain.Orders;
using OrderPipeline.Contracts;
using OrderPipeline.Domain.Ports;
using OrderPipeline.Functions.Adapters;

namespace OrderPipeline.Functions.Tests.Adapters;

public class DynamoDbOrderStorePlaceTests
{
    private const string TableName = "orders";

    private static readonly OrderThresholds Thresholds = new(10_000m, 50_000m);
    private static readonly DateTimeOffset PlacedAt = new(2026, 9, 24, 8, 30, 0, TimeSpan.Zero);

    private readonly IAmazonDynamoDB _client = Substitute.For<IAmazonDynamoDB>();

    private DynamoDbOrderStore Store() => new(_client, TableName, NullLogger<DynamoDbOrderStore>.Instance);

    private static Order OrderWithAmount(decimal amount) => Order.Place(
        "order-1",
        new PlaceOrderCommand("Alice", "Headphones", amount, "Express"),
        Thresholds,
        PlacedAt);

    private async Task<PutItemRequest> CaptureWrite(Order order)
    {
        await Store().PlaceAsync(order, CancellationToken.None);

        return (PutItemRequest)_client.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IAmazonDynamoDB.PutItemAsync))
            .GetArguments()[0]!;
    }

    [Fact]
    public async Task PlaceAsync_Always_WritesEveryAttributeThatListAsyncNeedsToReadItBack()
    {
        var request = await CaptureWrite(OrderWithAmount(1_499m));

        Assert.Equal("order-1", request.Item[OrderAttributes.OrderId].S);
        Assert.Equal("Alice", request.Item[OrderAttributes.CustomerName].S);
        Assert.Equal("Headphones", request.Item[OrderAttributes.Product].S);
        Assert.Equal("Express", request.Item[OrderAttributes.Notes].S);
        Assert.Equal("2026-09-24T08:30:00.0000000+00:00", request.Item[OrderAttributes.PlacedAt].S);
    }

    [Fact]
    public async Task PlaceAsync_Always_WritesAnItemThatToOrderCanReadBackUnchanged()
    {
        var placed = OrderWithAmount(25_000m);

        var request = await CaptureWrite(placed);
        _client.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new QueryResponse { Items = [request.Item] }));
        var listed = await Store().ListAsync(OrderPageRequest.First, CancellationToken.None);

        Assert.True(listed.IsSuccess);
        var readBack = Assert.Single(listed.Value!.Orders);
        Assert.Equivalent(placed, readBack, strict: true);
    }

    // The index orders orders by this attribute alone, so it has to sort the same way time does.
    // placedAt cannot be reused for it: that one carries an offset suffix, and text sorted by
    // "+00:00" against "Z" puts orders in an order nobody intended.
    [Fact]
    public async Task PlaceAsync_Always_WritesARecencyKeyThatSortsLikeTime()
    {
        var request = await CaptureWrite(OrderWithAmount(1_499m));

        Assert.Equal(RecencyBuckets.All, request.Item[OrderAttributes.RecencyBucket].S);
        Assert.Equal(
            "2026-09-24T08:30:00.0000000Z#order-1",
            request.Item[OrderAttributes.RecencyCursor].S);
    }

    [Fact]
    public async Task PlaceAsync_ForTwoOrdersInTheSameTick_StillTellsThemApartInTheIndex()
    {
        var first = await CaptureWrite(Order.Place(
            "order-a", new PlaceOrderCommand("Alice", "P", 1m, ""), Thresholds, PlacedAt));
        _client.ClearReceivedCalls();
        var second = await CaptureWrite(Order.Place(
            "order-b", new PlaceOrderCommand("Alice", "P", 1m, ""), Thresholds, PlacedAt));

        Assert.NotEqual(
            first.Item[OrderAttributes.RecencyCursor].S,
            second.Item[OrderAttributes.RecencyCursor].S);
    }

    [Fact]
    public async Task PlaceAsync_ForAHighValueOrder_WritesTheHighValueMarker()
    {
        var request = await CaptureWrite(OrderWithAmount(25_000m));

        Assert.Equal(ValueTiers.HighValue, request.Item["valueTier"].S);
    }

    [Fact]
    public async Task PlaceAsync_ForAnOrdinaryOrder_WritesTheStandardMarker()
    {
        var request = await CaptureWrite(OrderWithAmount(1_499m));

        Assert.Equal(ValueTiers.Standard, request.Item["valueTier"].S);
    }

    [Fact]
    public async Task PlaceAsync_AtExactlyTheHighValueThreshold_WritesTheStandardMarker()
    {
        var request = await CaptureWrite(OrderWithAmount(10_000m));

        Assert.Equal(ValueTiers.Standard, request.Item["valueTier"].S);
    }

    [Fact]
    public async Task PlaceAsync_Always_WritesTheMarkerAsAString()
    {
        var request = await CaptureWrite(OrderWithAmount(25_000m));

        Assert.NotNull(request.Item["valueTier"].S);
        Assert.Null(request.Item["valueTier"].N);
    }

    [Fact]
    public async Task PlaceAsync_Always_StartsTheOrderPending()
    {
        var request = await CaptureWrite(OrderWithAmount(1_499m));

        Assert.Equal("PENDING", request.Item["status"].S);
    }

    [Fact]
    public async Task PlaceAsync_Always_WritesAmountAsANumberWithInvariantFormatting()
    {
        var request = await CaptureWrite(OrderWithAmount(1_499.5m));

        Assert.Equal("1499.5", request.Item["amount"].N);
    }

    [Fact]
    public async Task PlaceAsync_Always_RefusesToOverwriteAnExistingOrder()
    {
        var request = await CaptureWrite(OrderWithAmount(1_499m));

        Assert.Equal("attribute_not_exists(orderId)", request.ConditionExpression);
    }

    [Fact]
    public async Task PlaceAsync_Always_TargetsTheConfiguredTable()
    {
        var request = await CaptureWrite(OrderWithAmount(1_499m));

        Assert.Equal(TableName, request.TableName);
    }

    [Fact]
    public async Task PlaceAsync_WhenTheOrderAlreadyExists_ReportsFailureInsteadOfThrowing()
    {
        _client
            .PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<PutItemResponse>>(_ => throw new ConditionalCheckFailedException("exists"));

        var result = await Store().PlaceAsync(OrderWithAmount(1_499m), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("already exists", result.Error);
    }

    [Fact]
    public async Task PlaceAsync_WhenTheTableIsUnreachable_ReportsFailureInsteadOfThrowing()
    {
        _client.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<PutItemResponse>>(_ => throw new AmazonDynamoDBException("table is gone"));

        var result = await Store().PlaceAsync(OrderWithAmount(1_499m), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("table is gone", result.Error);
    }

    [Fact]
    public async Task PlaceAsync_WhenTheInvocationRunsOutOfTime_ReportsFailureInsteadOfThrowing()
    {
        _client.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<PutItemResponse>>(_ => throw new OperationCanceledException());

        using var source = new CancellationTokenSource();
        source.Cancel();

        var result = await Store()
            .PlaceAsync(OrderWithAmount(1_499m), source.Token);

        Assert.False(result.IsSuccess);
        Assert.Contains("ran out of time", result.Error);
    }
}
