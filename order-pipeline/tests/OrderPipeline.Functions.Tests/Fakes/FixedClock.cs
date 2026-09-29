using OrderPipeline.Domain.Ports;

namespace OrderPipeline.Functions.Tests.Fakes;

public sealed class FixedClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}
