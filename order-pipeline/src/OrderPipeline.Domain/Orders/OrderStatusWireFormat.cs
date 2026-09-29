namespace OrderPipeline.Domain.Orders;

public static class OrderStatusWireFormat
{
    public static string ToWireFormat(this OrderStatus status) => status.ToString().ToUpperInvariant();

    public static OrderStatus FromWireFormat(string? value) =>
        Enum.TryParse<OrderStatus>(value, ignoreCase: true, out var status)
            ? status
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown order status.");
}
