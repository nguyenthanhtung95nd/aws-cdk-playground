using OrderPipeline.Domain.Orders;

namespace OrderPipeline.Domain.Tests.Orders;

public class OrderLifecycleTests
{
    [Theory]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Failed)]
    public void IsTerminal_WhenSettled_ReturnsTrue(OrderStatus status)
    {
        Assert.True(OrderLifecycle.IsTerminal(status));
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing)]
    public void IsTerminal_WhenStillMoving_ReturnsFalse(OrderStatus status)
    {
        Assert.False(OrderLifecycle.IsTerminal(status));
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Processing)]
    [InlineData(OrderStatus.Processing, OrderStatus.Completed)]
    [InlineData(OrderStatus.Processing, OrderStatus.Failed)]
    public void CanTransition_WhenMovingForward_ReturnsTrue(OrderStatus from, OrderStatus to)
    {
        Assert.True(OrderLifecycle.CanTransition(from, to));
    }

    [Theory]
    [InlineData(OrderStatus.Completed, OrderStatus.Processing)]
    [InlineData(OrderStatus.Failed, OrderStatus.Processing)]
    [InlineData(OrderStatus.Completed, OrderStatus.Failed)]
    [InlineData(OrderStatus.Failed, OrderStatus.Completed)]
    public void CanTransition_WhenLeavingTerminalState_ReturnsFalse(OrderStatus from, OrderStatus to)
    {
        Assert.False(OrderLifecycle.CanTransition(from, to));
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Completed)]
    [InlineData(OrderStatus.Pending, OrderStatus.Failed)]
    public void CanTransition_WhenSkippingProcessing_ReturnsFalse(OrderStatus from, OrderStatus to)
    {
        Assert.False(OrderLifecycle.CanTransition(from, to));
    }
}
