import type { Order } from '../types/order';

export interface OrderListProps {
  orders: Order[];
}

export function OrderList({ orders }: OrderListProps) {
  return (
    <div className="orders-scroll" tabIndex={0} role="region" aria-label="Orders table">
      <table className="orders">
        <caption>Orders and where each one has reached in the pipeline</caption>
        <thead>
          <tr>
            <th scope="col">Customer</th>
            <th scope="col">Product</th>
            <th scope="col" className="amount">Amount</th>
            <th scope="col">Status</th>
          </tr>
        </thead>
        <tbody>
          {orders.map((order) => (
            <tr key={order.orderId}>
              <td>{order.customerName}</td>
              <td>
                {order.product}
                {order.isHighValue ? <span className="tag">high value</span> : null}
              </td>
              <td className="amount">{order.amount.toLocaleString()}</td>
              <td>
                <span className={`status status-${order.status.toLowerCase()}`}>{order.status}</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
