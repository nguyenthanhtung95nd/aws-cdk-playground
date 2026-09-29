using OrderPipeline.Domain.Orders;

namespace OrderPipeline.Domain.Tests.Orders;

public class BusinessLimitRuleTests
{
    private readonly OrderThresholds _thresholds = new(10_000m, 50_000m);

    [Fact]
    public void RejectionOf_WhenTheAmountIsOverTheLimit_ExplainsWhyInBusinessTerms()
    {
        var rejection = BusinessLimitRule.RejectionOf(OrderFor(50_000.01m), _thresholds);

        Assert.NotNull(rejection);
        Assert.Equal(FailureKind.Business, rejection.Kind);
        Assert.Contains("50000.01", rejection.Reason);
        Assert.Contains("business limit", rejection.Reason);
    }

    [Fact]
    public void RejectionOf_AtExactlyTheLimit_AcceptsTheOrder()
    {
        Assert.Null(BusinessLimitRule.RejectionOf(OrderFor(50_000m), _thresholds));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10_000)]
    [InlineData(49_999.99)]
    public void RejectionOf_BelowTheLimit_AcceptsTheOrder(decimal amount)
    {
        Assert.Null(BusinessLimitRule.RejectionOf(OrderFor(amount), _thresholds));
    }

    [Fact]
    public void RejectionOf_ForAHighValueOrderThatIsStillWithinTheLimit_AcceptsIt()
    {
        var order = OrderFor(20_000m) with { IsHighValue = true };

        Assert.Null(BusinessLimitRule.RejectionOf(order, _thresholds));
    }

    private static Order OrderFor(decimal amount) => new()
    {
        OrderId = "order-1",
        CustomerName = "Ada",
        Product = "Keyboard",
        Amount = amount,
        Notes = string.Empty,
        Status = OrderStatus.Processing,
        IsHighValue = false,
        PlacedAt = DateTimeOffset.UnixEpoch
    };
}
