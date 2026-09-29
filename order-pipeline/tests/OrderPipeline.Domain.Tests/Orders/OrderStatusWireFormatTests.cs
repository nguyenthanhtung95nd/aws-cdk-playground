using OrderPipeline.Domain.Orders;

namespace OrderPipeline.Domain.Tests.Orders;

public class OrderStatusWireFormatTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, "PENDING")]
    [InlineData(OrderStatus.Processing, "PROCESSING")]
    [InlineData(OrderStatus.Completed, "COMPLETED")]
    [InlineData(OrderStatus.Failed, "FAILED")]
    public void ToWireFormat_Always_UsesScreamingCase(OrderStatus status, string expected)
    {
        Assert.Equal(expected, status.ToWireFormat());
    }

    [Theory]
    [InlineData("PENDING", OrderStatus.Pending)]
    [InlineData("completed", OrderStatus.Completed)]
    [InlineData("Failed", OrderStatus.Failed)]
    public void FromWireFormat_WhateverTheCasing_ReadsTheStatusBack(string wire, OrderStatus expected)
    {
        Assert.Equal(expected, OrderStatusWireFormat.FromWireFormat(wire));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SHIPPED")]
    public void FromWireFormat_WithAnUnknownStatus_Throws(string? wire)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrderStatusWireFormat.FromWireFormat(wire));
    }
}
