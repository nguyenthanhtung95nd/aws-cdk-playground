using OrderPipeline.Domain.Ports;

namespace OrderPipeline.Functions.Tests.Fakes;

public sealed class FixedOrderIdGenerator(string orderId) : IOrderIdGenerator
{
    public string Next() => orderId;
}
