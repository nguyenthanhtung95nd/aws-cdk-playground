import { afterEach, describe, expect, it, vi } from 'vitest';
import { OrderDataService, REQUEST_TIMEOUT_MS } from '../../src/services/OrderDataService';

function respondWith(status: number, body: unknown) {
  const fetched = vi.fn((url: string | URL) => {
    void url;
    return Promise.resolve(new Response(JSON.stringify(body), { status }));
  });
  vi.stubGlobal('fetch', fetched);
  return fetched;
}

afterEach(() => vi.unstubAllGlobals());

describe('OrderDataService', () => {
  it('returns the orders the api sends', async () => {
    respondWith(200, {
      orders: [
        {
          orderId: 'a',
          customerName: 'Alice',
          product: 'Headphones',
          amount: 1499,
          notes: '',
          status: 'PENDING',
          isHighValue: false,
          placedAt: '2026-09-24T08:00:00Z',
        },
      ],
    });

    const page = await new OrderDataService().list();

    expect(page.orders).toHaveLength(1);
    expect(page.orders[0].orderId).toBe('a');
  });

  it('asks for the whole first page when nobody says otherwise', async () => {
    const fetched = respondWith(200, { orders: [] });

    await new OrderDataService().list();

    expect(String(fetched.mock.calls[0][0])).not.toContain('?');
  });

  it('passes a limit and a cursor through to the api', async () => {
    const fetched = respondWith(200, { orders: [] });

    await new OrderDataService().list({ limit: 10, cursor: 'a+b/c=' });

    const url = String(fetched.mock.calls[0][0]);
    expect(url).toContain('limit=10');
    expect(url).toContain(`cursor=${encodeURIComponent('a+b/c=')}`);
  });

  it('surfaces the api validation message when placing fails', async () => {
    respondWith(400, { message: 'customerName is required.' });

    await expect(
      new OrderDataService().place({ customerName: '', product: 'p', amount: 1, notes: '' }),
    ).rejects.toThrow('customerName is required.');
  });

  it('refuses a body the app cannot understand instead of passing it on', async () => {
    respondWith(200, { orders: [{ orderId: 'a', status: 'PENDING' }] });

    await expect(new OrderDataService().list()).rejects.toThrow(
      'shape this app does not understand',
    );
  });

  it('falls back to a readable message when the api sends no body', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(new Response('', { status: 500 }))));

    await expect(new OrderDataService().list()).rejects.toThrow('Could not load orders (500)');
  });

  it('gives every request a signal so nothing can hang forever', async () => {
    const signals: (AbortSignal | null | undefined)[] = [];
    vi.stubGlobal(
      'fetch',
      vi.fn((_url: string, init?: RequestInit) => {
        signals.push(init?.signal);
        const body = init?.method === 'POST' ? { orderId: 'a', status: 'PENDING' } : { orders: [] };
        return Promise.resolve(new Response(JSON.stringify(body), { status: 200 }));
      }),
    );

    await new OrderDataService().list();
    await new OrderDataService().place({ customerName: 'A', product: 'B', amount: 1, notes: '' });

    expect(signals).toHaveLength(2);
    for (const signal of signals) {
      expect(signal).toBeInstanceOf(AbortSignal);
    }
  });

  it('abandons the request when the caller aborts', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        (_url: string, init?: RequestInit) =>
          new Promise<Response>((_resolve, reject) => {
            init?.signal?.addEventListener('abort', () => {
              reject(new DOMException('aborted', 'AbortError'));
            });
          }),
      ),
    );

    const controller = new AbortController();
    const pending = new OrderDataService().list(undefined, controller.signal);
    controller.abort();

    await expect(pending).rejects.toThrow('aborted');
  });

  it('gives up on its own once the api stays silent past the timeout', async () => {
    vi.useFakeTimers();
    vi.stubGlobal(
      'fetch',
      vi.fn(
        (_url: string, init?: RequestInit) =>
          new Promise<Response>((_resolve, reject) => {
            init?.signal?.addEventListener('abort', () => {
              reject(new Error('The API did not answer within 10000ms'));
            });
          }),
      ),
    );

    const pending = new OrderDataService().list();
    const settled = expect(pending).rejects.toThrow('did not answer within 10000ms');
    await vi.advanceTimersByTimeAsync(REQUEST_TIMEOUT_MS);
    await settled;

    vi.useRealTimers();
  });
});
