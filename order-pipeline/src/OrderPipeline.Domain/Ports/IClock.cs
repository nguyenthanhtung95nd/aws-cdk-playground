namespace OrderPipeline.Domain.Ports;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
