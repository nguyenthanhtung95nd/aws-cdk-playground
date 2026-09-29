import { describe, expectTypeOf, it } from 'vitest';
import type { OrdersState } from '../../src/hooks/useOrders';

type Ready = Extract<OrdersState, { status: 'ready' }>;
type Failed = Extract<OrdersState, { status: 'failed' }>;
type Loading = Extract<OrdersState, { status: 'loading' }>;

describe('OrdersState', () => {
  it('cannot be ready without orders', () => {
    // @ts-expect-error a ready state must carry the orders it is ready with
    const invalid: OrdersState = { status: 'ready', staleError: null };
    void invalid;
    expectTypeOf<Ready>().toHaveProperty('orders');
  });

  it('cannot be failed without an error', () => {
    // @ts-expect-error a failed state must say what failed
    const invalid: OrdersState = { status: 'failed' };
    void invalid;
    expectTypeOf<Failed>().toHaveProperty('error');
  });

  it('cannot carry an error while loading', () => {
    // @ts-expect-error loading has no error to report yet
    const invalid: OrdersState = { status: 'loading', error: 'boom' };
    void invalid;
    expectTypeOf<Loading>().not.toHaveProperty('error');
  });
});
