namespace OrderPipeline.Domain.Orders;

// The parking area is only worth building if it can be seen working, and nothing else in this
// system fails on demand. An order carrying this marker fails every attempt on purpose, which is
// what lets the retry budget run out and the settler be watched doing its job.
public static class FailureProbe
{
    public const string Marker = "FAIL_ON_PURPOSE";

    public const string Reason = "The order asked to fail on purpose.";

    public static bool IsRequestedBy(Order order) =>
        order.Notes.Contains(Marker, StringComparison.OrdinalIgnoreCase);
}
