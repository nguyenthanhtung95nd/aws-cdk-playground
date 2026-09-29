using System.Globalization;

namespace OrderPipeline.Domain.Orders;

public static class BusinessLimitRule
{
    public static OrderFailure? RejectionOf(Order order, OrderThresholds thresholds) =>
        thresholds.ExceedsBusinessLimit(order.Amount)
            ? new OrderFailure(
                FailureKind.Business,
                $"The order is for {Money(order.Amount)}, which is over the business limit of "
                + $"{Money(thresholds.BusinessLimit)}.")
            : null;

    private static string Money(decimal amount) => amount.ToString("0.##", CultureInfo.InvariantCulture);
}
