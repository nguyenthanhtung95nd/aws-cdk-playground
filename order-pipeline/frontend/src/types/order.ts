export type OrderStatus = 'PENDING' | 'PROCESSING' | 'COMPLETED' | 'FAILED';

const settled: readonly OrderStatus[] = ['COMPLETED', 'FAILED'];

export function isTerminal(status: OrderStatus): boolean {
  return settled.includes(status);
}

export interface Order {
  orderId: string;
  customerName: string;
  product: string;
  amount: number;
  notes: string;
  status: OrderStatus;
  isHighValue: boolean;
  placedAt: string;
}

export interface OrderPage {
  orders: Order[];
  nextCursor: string | null;
}

export interface NewOrder {
  customerName: string;
  product: string;
  amount: number;
  notes: string;
}

export interface PlacedOrder {
  orderId: string;
  status: OrderStatus;
}
