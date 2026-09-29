import type { Order, OrderPage, OrderStatus, PlacedOrder } from '../types/order';

const orderStatuses: readonly OrderStatus[] = ['PENDING', 'PROCESSING', 'COMPLETED', 'FAILED'];

const shapeMismatch = 'The API sent an order in a shape this app does not understand.';

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function isOrderStatus(value: unknown): value is OrderStatus {
  return orderStatuses.some((status) => status === value);
}

export function decodeOrderPage(value: unknown): OrderPage {
  if (!isRecord(value) || !Array.isArray(value.orders)) {
    throw new Error('The API sent something that is not a page of orders.');
  }

  return { orders: value.orders.map(decodeOrder), nextCursor: decodeCursor(value.nextCursor) };
}

// The API leaves the cursor out entirely when there is nothing more to fetch, so its absence is
// an answer rather than a gap.
function decodeCursor(value: unknown): string | null {
  if (value === undefined || value === null) {
    return null;
  }

  if (typeof value !== 'string') {
    throw new Error(shapeMismatch);
  }

  return value;
}

export function decodeOrder(value: unknown): Order {
  if (
    !isRecord(value) ||
    typeof value.orderId !== 'string' ||
    typeof value.customerName !== 'string' ||
    typeof value.product !== 'string' ||
    typeof value.amount !== 'number' ||
    typeof value.notes !== 'string' ||
    !isOrderStatus(value.status) ||
    typeof value.isHighValue !== 'boolean' ||
    typeof value.placedAt !== 'string'
  ) {
    throw new Error(shapeMismatch);
  }

  return {
    orderId: value.orderId,
    customerName: value.customerName,
    product: value.product,
    amount: value.amount,
    notes: value.notes,
    status: value.status,
    isHighValue: value.isHighValue,
    placedAt: value.placedAt,
  };
}

export function decodePlacedOrder(value: unknown): PlacedOrder {
  if (!isRecord(value) || typeof value.orderId !== 'string' || !isOrderStatus(value.status)) {
    throw new Error(shapeMismatch);
  }

  return { orderId: value.orderId, status: value.status };
}

export function decodeFailureMessage(value: unknown): string | null {
  return isRecord(value) && typeof value.message === 'string' ? value.message : null;
}
