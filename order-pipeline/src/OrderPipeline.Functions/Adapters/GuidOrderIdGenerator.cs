using OrderPipeline.Domain.Ports;

namespace OrderPipeline.Functions.Adapters;

public sealed class GuidOrderIdGenerator : IOrderIdGenerator
{
    public string Next() => Guid.NewGuid().ToString();
}
