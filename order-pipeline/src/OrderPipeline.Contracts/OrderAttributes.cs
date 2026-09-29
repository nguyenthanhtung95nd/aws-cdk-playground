namespace OrderPipeline.Contracts;

public static class OrderAttributes
{
    public const string OrderId = "orderId";
    public const string CustomerName = "customerName";
    public const string Product = "product";
    public const string Amount = "amount";
    public const string Notes = "notes";
    public const string Status = "status";
    public const string ValueTier = "valueTier";
    public const string PlacedAt = "placedAt";
    public const string FailureReason = "failureReason";
    public const string FailureKind = "failureKind";
    public const string RecencyBucket = "recencyBucket";
    public const string RecencyCursor = "recencyCursor";
}
