import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { createOrderGateway } from '../services/createServices';
import type { NewOrder, Order } from '../types/order';
import {
  afterFailure,
  afterSuccess,
  nextDelay,
  startedActive,
  type PollRhythm,
} from './pollSchedule';

export { POLL_INTERVAL_MS } from './pollSchedule';

export type OrdersState =
  | { status: 'loading' }
  | { status: 'failed'; error: string }
  | { status: 'ready'; orders: Order[]; hasMore: boolean; staleError: string | null };

export interface UseOrdersResult {
  state: OrdersState;
  place: (input: NewOrder) => Promise<void>;
  reload: () => Promise<void>;
}

function describe(cause: unknown): string {
  return cause instanceof Error ? cause.message : 'Could not load orders';
}

export function useOrders(): UseOrdersResult {
  const gateway = useMemo(() => createOrderGateway(), []);
  const [state, setState] = useState<OrdersState>({ status: 'loading' });
  const newest = useRef(0);
  const inFlight = useRef<AbortController | null>(null);
  const rhythm = useRef<PollRhythm>(startedActive(Date.now()));
  const reschedule = useRef<() => void>(() => undefined);

  const dropAnythingStillInFlight = useCallback(() => {
    newest.current++;
    inFlight.current?.abort();
  }, []);

  const load = useCallback(async () => {
    inFlight.current?.abort();
    const controller = new AbortController();
    inFlight.current = controller;
    const request = ++newest.current;

    try {
      // Only the newest page: this view exists to watch an order move, and an order that has
      // fallen off the first page stopped moving long ago.
      const page = await gateway.list(undefined, controller.signal);
      if (request !== newest.current) {
        return;
      }
      rhythm.current = afterSuccess(page.orders, rhythm.current, Date.now());
      setState({
        status: 'ready',
        orders: page.orders,
        hasMore: page.nextCursor !== null,
        staleError: null,
      });
    } catch (cause) {
      if (request !== newest.current) {
        return;
      }
      rhythm.current = afterFailure(rhythm.current);
      setState((current) =>
        current.status === 'ready'
          ? { ...current, staleError: describe(cause) }
          : { status: 'failed', error: describe(cause) },
      );
    }
  }, [gateway]);

  const reload = useCallback(async () => {
    rhythm.current = startedActive(Date.now());
    await load();
    reschedule.current();
  }, [load]);

  useEffect(() => {
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | undefined;

    // A hidden or offline tab gets no timer at all rather than a timer that wakes up to do
    // nothing, so a window left open overnight costs exactly nothing.
    const asleep = () => cancelled || document.hidden || navigator.onLine === false;

    const stop = () => {
      clearTimeout(timer);
      timer = undefined;
    };

    const schedule = () => {
      stop();
      if (asleep()) {
        return;
      }
      const delay = nextDelay(rhythm.current, Date.now());
      if (delay !== null) {
        timer = setTimeout(() => void run(), delay);
      }
    };

    const run = async () => {
      if (asleep()) {
        return;
      }
      await load();
      schedule();
    };

    const wake = () => {
      if (asleep()) {
        stop();
        return;
      }
      rhythm.current = startedActive(Date.now());
      void run();
    };

    reschedule.current = schedule;

    void run();
    document.addEventListener('visibilitychange', wake);
    window.addEventListener('focus', wake);
    window.addEventListener('online', wake);

    return () => {
      cancelled = true;
      stop();
      dropAnythingStillInFlight();
      document.removeEventListener('visibilitychange', wake);
      window.removeEventListener('focus', wake);
      window.removeEventListener('online', wake);
    };
  }, [load, dropAnythingStillInFlight]);

  const place = useCallback(
    async (input: NewOrder) => {
      await gateway.place(input);
      await reload();
    },
    [gateway, reload],
  );

  return { state, place, reload };
}
