namespace OrderPipeline.Domain.Orders;

public sealed record Order
{
    public required string OrderId { get; init; }

    public required string CustomerName { get; init; }

    public required string Product { get; init; }

    public required decimal Amount { get; init; }

    public required string Notes { get; init; }

    public required OrderStatus Status { get; init; }

    public required bool IsHighValue { get; init; }

    public required DateTimeOffset PlacedAt { get; init; }

    public static Order Place(
        string orderId,
        PlaceOrderCommand command,
        OrderThresholds thresholds,
        DateTimeOffset placedAt) => new()
        {
            OrderId = orderId,
            CustomerName = command.CustomerName,
            Product = command.Product,
            Amount = command.Amount,
            Notes = command.Notes,
            Status = OrderStatus.Pending,
            IsHighValue = thresholds.IsHighValue(command.Amount),
            PlacedAt = placedAt
        };
}
