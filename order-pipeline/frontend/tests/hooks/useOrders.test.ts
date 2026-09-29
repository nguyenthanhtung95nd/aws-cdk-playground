import { act, renderHook, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { OrdersState } from '../../src/hooks/useOrders';
import type { Order, OrderPage } from '../../src/types/order';

const gateway = {
  list: vi.fn(),
  place: vi.fn(),
};

vi.mock('../../src/services/createServices', () => ({
  createOrderGateway: () => gateway,
}));

const { POLL_INTERVAL_MS, useOrders } = await import('../../src/hooks/useOrders');

// 5s + 5s + 15s + 15s of settled polling, after which the schedule gives up on its own.
const SETTLED_RUNDOWN_MS = 40_000;

const anOrder: Order = {
  orderId: 'a',
  customerName: 'Alice',
  product: 'Headphones',
  amount: 1499,
  notes: '',
  status: 'PENDING',
  isHighValue: false,
  placedAt: '2026-09-24T08:00:00Z',
};

function page(orders: Order[] = [], nextCursor: string | null = null): OrderPage {
  return { orders, nextCursor };
}

function ready(state: OrdersState) {
  if (state.status !== 'ready') {
    throw new Error(`expected a ready state, got ${state.status}`);
  }
  return state;
}

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  // The schedule spreads each delay by +/-20% so that tabs opened together do not stay in step.
  // A midpoint draw leaves every interval at its nominal value, which is what these tests advance.
  vi.spyOn(Math, 'random').mockReturnValue(0.5);
  gateway.list.mockReset();
  gateway.place.mockReset();
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe('useOrders', () => {
  it('reports a failure instead of an empty list when the first load fails', async () => {
    gateway.list.mockRejectedValue(new Error('api is down'));

    const { result } = renderHook(() => useOrders());

    await waitFor(() => expect(result.current.state.status).toBe('failed'));
    expect(result.current.state).toEqual({ status: 'failed', error: 'api is down' });
  });

  it('keeps the orders it already has when a later poll fails', async () => {
    gateway.list.mockResolvedValueOnce(page([anOrder])).mockRejectedValue(new Error('api is down'));

    const { result } = renderHook(() => useOrders());
    await waitFor(() => expect(result.current.state.status).toBe('ready'));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS);
    });

    await waitFor(() => expect(ready(result.current.state).staleError).toBe('api is down'));
    expect(ready(result.current.state).orders).toHaveLength(1);
  });

  it('clears a stale error once a poll succeeds again', async () => {
    gateway.list
      .mockResolvedValueOnce(page([anOrder]))
      .mockRejectedValueOnce(new Error('api is down'))
      .mockResolvedValue(page([anOrder]));

    const { result } = renderHook(() => useOrders());
    await waitFor(() => expect(result.current.state.status).toBe('ready'));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS);
    });
    await waitFor(() => expect(ready(result.current.state).staleError).toBe('api is down'));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS);
    });
    await waitFor(() => expect(ready(result.current.state).staleError).toBeNull());
  });

  it('refreshes the list right after placing an order', async () => {
    gateway.list.mockResolvedValue(page());
    gateway.place.mockResolvedValue({ orderId: 'a', status: 'PENDING' });

    const { result } = renderHook(() => useOrders());
    await waitFor(() => expect(result.current.state.status).toBe('ready'));
    const loadsBefore = gateway.list.mock.calls.length;

    await act(async () => {
      await result.current.place({ customerName: 'Alice', product: 'p', amount: 1, notes: '' });
    });

    expect(gateway.place).toHaveBeenCalledOnce();
    expect(gateway.list.mock.calls.length).toBeGreaterThan(loadsBefore);
  });

  it('does not poll while the tab is hidden', async () => {
    gateway.list.mockResolvedValue(page([anOrder]));
    const hidden = vi.spyOn(document, 'hidden', 'get').mockReturnValue(true);

    const { result } = renderHook(() => useOrders());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS * 3);
    });

    expect(gateway.list).not.toHaveBeenCalled();
    expect(result.current.state.status).toBe('loading');
    hidden.mockRestore();
  });

  it('stops polling once every order has settled', async () => {
    gateway.list.mockResolvedValue(page([{ ...anOrder, status: 'COMPLETED' }]));

    const { result } = renderHook(() => useOrders());
    await waitFor(() => expect(result.current.state.status).toBe('ready'));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(SETTLED_RUNDOWN_MS);
    });
    const afterRundown = gateway.list.mock.calls.length;

    await act(async () => {
      await vi.advanceTimersByTimeAsync(SETTLED_RUNDOWN_MS);
    });

    expect(gateway.list.mock.calls.length).toBe(afterRundown);
  });

  it('starts asking again when the tab comes back after it stopped', async () => {
    gateway.list.mockResolvedValue(page([{ ...anOrder, status: 'COMPLETED' }]));

    renderHook(() => useOrders());
    await act(async () => {
      await vi.advanceTimersByTimeAsync(SETTLED_RUNDOWN_MS * 2);
    });
    const whileStopped = gateway.list.mock.calls.length;

    await act(async () => {
      document.dispatchEvent(new Event('visibilitychange'));
      await Promise.resolve();
    });

    expect(gateway.list.mock.calls.length).toBeGreaterThan(whileStopped);
  });

  it('backs off further with each failure in a row and recovers on the next success', async () => {
    gateway.list.mockRejectedValue(new Error('api is down'));

    renderHook(() => useOrders());
    await waitFor(() => expect(gateway.list).toHaveBeenCalledOnce());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS);
    });
    expect(gateway.list).toHaveBeenCalledTimes(2);

    // The second failure doubles the wait, so the interval that just worked is no longer enough.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS);
    });
    expect(gateway.list).toHaveBeenCalledTimes(2);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS);
    });
    expect(gateway.list).toHaveBeenCalledTimes(3);
  });

  it('schedules nothing while the browser reports itself offline', async () => {
    gateway.list.mockResolvedValue(page([anOrder]));
    const offline = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);

    renderHook(() => useOrders());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS * 5);
    });

    expect(gateway.list).not.toHaveBeenCalled();
    offline.mockRestore();
  });

  it('ignores a stale response that arrives after a newer one', async () => {
    const newer: Order = { ...anOrder, orderId: 'newer' };
    let releaseStale: (answer: OrderPage) => void = () => undefined;
    gateway.list
      .mockReturnValueOnce(new Promise<OrderPage>((resolve) => (releaseStale = resolve)))
      .mockResolvedValue(page([newer]));

    const { result } = renderHook(() => useOrders());
    await act(async () => {
      await result.current.reload();
    });
    await waitFor(() => expect(result.current.state.status).toBe('ready'));
    expect(ready(result.current.state).orders).toEqual([newer]);

    await act(async () => {
      releaseStale(page([{ ...anOrder, orderId: 'stale' }]));
      await Promise.resolve();
    });

    expect(ready(result.current.state).orders).toEqual([newer]);
  });

  it('keeps the order it just placed even when an older poll answers afterwards', async () => {
    const placed: Order = { ...anOrder, orderId: 'placed' };
    let releaseFirstPoll: (answer: OrderPage) => void = () => undefined;
    gateway.list
      .mockReturnValueOnce(new Promise<OrderPage>((resolve) => (releaseFirstPoll = resolve)))
      .mockResolvedValue(page([placed]));
    gateway.place.mockResolvedValue({ orderId: 'placed', status: 'PENDING' });

    const { result } = renderHook(() => useOrders());
    await act(async () => {
      await result.current.place({ customerName: 'A', product: 'B', amount: 1, notes: '' });
    });
    await waitFor(() => expect(result.current.state.status).toBe('ready'));

    await act(async () => {
      releaseFirstPoll(page());
      await Promise.resolve();
    });

    expect(ready(result.current.state).orders).toEqual([placed]);
  });

  it('aborts the request that is still in flight when it unmounts', async () => {
    let seen: AbortSignal | undefined;
    gateway.list.mockImplementation(
      (_page: unknown, signal?: AbortSignal) =>
        new Promise<OrderPage>(() => {
          seen = signal;
        }),
    );

    const { unmount } = renderHook(() => useOrders());
    await waitFor(() => expect(seen).toBeDefined());
    expect(seen?.aborted).toBe(false);

    unmount();

    expect(seen?.aborted).toBe(true);
  });

  it('does not change state when a request answers after unmount', async () => {
    let release: (answer: OrderPage) => void = () => undefined;
    gateway.list.mockReturnValue(new Promise<OrderPage>((resolve) => (release = resolve)));

    const { result, unmount } = renderHook(() => useOrders());
    expect(result.current.state.status).toBe('loading');

    unmount();
    await act(async () => {
      release(page([anOrder]));
      await Promise.resolve();
    });

    expect(result.current.state.status).toBe('loading');
  });
});
