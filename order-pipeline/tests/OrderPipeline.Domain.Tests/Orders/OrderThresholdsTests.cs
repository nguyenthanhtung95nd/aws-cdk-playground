using OrderPipeline.Domain.Orders;

namespace OrderPipeline.Domain.Tests.Orders;

public class OrderThresholdsTests
{
    [Fact]
    public void Constructor_WhenBusinessLimitIsNotAboveHighValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrderThresholds(10_000m, 10_000m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrderThresholds(10_000m, 5_000m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenHighValueIsNotPositive_Throws(int highValue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrderThresholds(highValue, 50_000m));
    }

    [Fact]
    public void IsHighValue_AtExactlyTheThreshold_IsFalse()
    {
        var thresholds = new OrderThresholds(10_000m, 50_000m);

        Assert.False(thresholds.IsHighValue(10_000m));
        Assert.True(thresholds.IsHighValue(10_000.01m));
    }

    [Fact]
    public void ExceedsBusinessLimit_AtExactlyTheLimit_IsFalse()
    {
        var thresholds = new OrderThresholds(10_000m, 50_000m);

        Assert.False(thresholds.ExceedsBusinessLimit(50_000m));
        Assert.True(thresholds.ExceedsBusinessLimit(50_000.01m));
    }
}
