namespace OrderPipeline.Functions.Responses;

public sealed record OrderPageResponse(IReadOnlyList<OrderResponse> Orders, string? NextCursor);
