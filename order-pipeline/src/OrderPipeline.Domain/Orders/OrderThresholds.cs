namespace OrderPipeline.Domain.Orders;

public sealed class OrderThresholds
{
    public OrderThresholds(decimal highValue, decimal businessLimit)
    {
        if (highValue <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(highValue), highValue, "The high-value threshold must be greater than zero.");
        }

        if (businessLimit <= highValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(businessLimit), businessLimit,
                "The business limit must be greater than the high-value threshold, otherwise every "
                + "high-value order would already be rejected and the review flow could never run.");
        }

        HighValue = highValue;
        BusinessLimit = businessLimit;
    }

    public decimal HighValue { get; }

    public decimal BusinessLimit { get; }

    public bool IsHighValue(decimal amount) => amount > HighValue;

    public bool ExceedsBusinessLimit(decimal amount) => amount > BusinessLimit;
}
