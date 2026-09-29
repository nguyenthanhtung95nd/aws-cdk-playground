namespace OrderPipeline.Domain.Orders;

public sealed record OrderPage(IReadOnlyList<Order> Orders, string? ResumeFrom);
