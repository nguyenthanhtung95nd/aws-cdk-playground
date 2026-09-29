using OrderPipeline.Domain.Ports;

namespace OrderPipeline.Functions.Adapters;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
