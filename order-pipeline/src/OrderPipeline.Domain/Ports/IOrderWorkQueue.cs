using OrderPipeline.Domain.Common;

namespace OrderPipeline.Domain.Ports;

public interface IOrderWorkQueue
{
    Task<Result<string>> EnqueueAsync(string orderId, CancellationToken cancellationToken);
}
