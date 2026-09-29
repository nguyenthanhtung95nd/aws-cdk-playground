namespace OrderPipeline.Domain.Orders;

public static class FailureKindWireFormat
{
    public static string ToWireFormat(this FailureKind kind) => kind.ToString().ToUpperInvariant();
}
