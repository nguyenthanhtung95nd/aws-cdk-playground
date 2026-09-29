namespace OrderPipeline.Contracts;

public static class RecencyBuckets
{
    // Every order shares one bucket so that "the newest orders" is a single query rather than a
    // scan. Splitting it across several would only be worth the merge once writes approach the
    // limit of a single partition, which is a few hundred orders a second.
    public const string All = "ALL";
}
