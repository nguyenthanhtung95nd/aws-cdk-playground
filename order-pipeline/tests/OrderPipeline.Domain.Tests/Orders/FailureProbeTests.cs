using OrderPipeline.Domain.Orders;

namespace OrderPipeline.Domain.Tests.Orders;

public class FailureProbeTests
{
    [Theory]
    [InlineData("FAIL_ON_PURPOSE")]
    [InlineData("fail_on_purpose")]
    [InlineData("please FAIL_ON_PURPOSE so I can watch it")]
    public void IsRequestedBy_WhenTheNotesCarryTheMarker_ReturnsTrue(string notes)
    {
        Assert.True(FailureProbe.IsRequestedBy(OrderWith(notes)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("deliver before friday")]
    [InlineData("FAIL")]
    public void IsRequestedBy_WithoutTheMarker_ReturnsFalse(string notes)
    {
        Assert.False(FailureProbe.IsRequestedBy(OrderWith(notes)));
    }

    private static Order OrderWith(string notes) => new()
    {
        OrderId = "order-1",
        CustomerName = "Ada",
        Product = "Keyboard",
        Amount = 900m,
        Notes = notes,
        Status = OrderStatus.Processing,
        IsHighValue = false,
        PlacedAt = DateTimeOffset.UnixEpoch
    };
}
