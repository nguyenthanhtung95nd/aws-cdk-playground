namespace OrderPipeline.Domain.Orders;

public static class OrderLifecycle
{
    public static bool IsTerminal(OrderStatus status) =>
        status is OrderStatus.Completed or OrderStatus.Failed;

    public static bool CanTransition(OrderStatus from, OrderStatus to) => (from, to) switch
    {
        (OrderStatus.Pending, OrderStatus.Processing) => true,
        (OrderStatus.Processing, OrderStatus.Completed) => true,
        (OrderStatus.Processing, OrderStatus.Failed) => true,
        _ => false
    };
}
