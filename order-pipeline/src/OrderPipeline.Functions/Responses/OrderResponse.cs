namespace OrderPipeline.Functions.Responses;

public sealed record OrderResponse(
    string OrderId,
    string CustomerName,
    string Product,
    decimal Amount,
    string Notes,
    string Status,
    bool IsHighValue,
    string PlacedAt);
