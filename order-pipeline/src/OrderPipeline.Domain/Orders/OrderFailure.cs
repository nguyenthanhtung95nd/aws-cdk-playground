namespace OrderPipeline.Domain.Orders;

public sealed record OrderFailure(FailureKind Kind, string Reason);
