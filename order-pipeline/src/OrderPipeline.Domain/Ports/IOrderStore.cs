using OrderPipeline.Domain.Common;
using OrderPipeline.Domain.Orders;

namespace OrderPipeline.Domain.Ports;

public interface IOrderStore
{
    Task<Result<Order>> PlaceAsync(Order order, CancellationToken cancellationToken);

    Task<Result<Order>> GetAsync(string orderId, CancellationToken cancellationToken);

    Task<Result<OrderPage>> ListAsync(OrderPageRequest page, CancellationToken cancellationToken);

    Task<Result<OrderTransition>> TransitionAsync(
        string orderId,
        OrderStatus from,
        OrderStatus to,
        CancellationToken cancellationToken);

    Task<Result<OrderTransition>> FailAsync(
        string orderId,
        OrderStatus from,
        OrderFailure failure,
        CancellationToken cancellationToken);
}
