using OrderPipeline.Domain.Orders;

namespace OrderPipeline.Domain.Tests.Orders;

public class OrderPlacementTests
{
    private static readonly OrderThresholds Thresholds = new(10_000m, 50_000m);
    private static readonly DateTimeOffset PlacedAt = new(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);

    private static Order PlaceWithAmount(decimal amount) => Order.Place(
        "order-1",
        new PlaceOrderCommand("Alice", "Headphones", amount, string.Empty),
        Thresholds,
        PlacedAt);

    [Fact]
    public void Place_Always_StartsPending()
    {
        Assert.Equal(OrderStatus.Pending, PlaceWithAmount(1m).Status);
    }

    [Fact]
    public void Place_AtExactlyTheHighValueThreshold_IsNotHighValue()
    {
        Assert.False(PlaceWithAmount(10_000m).IsHighValue);
    }

    [Fact]
    public void Place_JustAboveTheHighValueThreshold_IsHighValue()
    {
        Assert.True(PlaceWithAmount(10_000.01m).IsHighValue);
    }

    [Fact]
    public void Place_AboveTheBusinessLimit_IsStillHighValueAndStillPending()
    {
        var order = PlaceWithAmount(60_000m);

        Assert.True(order.IsHighValue);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }
}
