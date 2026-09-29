import type { NewOrder, OrderPage, PlacedOrder } from '../types/order';

export interface PageRequest {
  limit?: number;
  cursor?: string;
}

export interface OrderGateway {
  list(page?: PageRequest, signal?: AbortSignal): Promise<OrderPage>;
  place(input: NewOrder, signal?: AbortSignal): Promise<PlacedOrder>;
}
