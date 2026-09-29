import { config } from '../config';
import type { NewOrder, OrderPage, PlacedOrder } from '../types/order';
import { decodeFailureMessage, decodeOrderPage, decodePlacedOrder } from './decode';
import type { OrderGateway, PageRequest } from './gateways';

export const REQUEST_TIMEOUT_MS = 10_000;

export class OrderDataService implements OrderGateway {
  private readonly baseUrl = config.apiBaseUrl;

  async list(page?: PageRequest, signal?: AbortSignal): Promise<OrderPage> {
    const deadline = startDeadline(signal);
    try {
      const response = await fetch(`${this.baseUrl}/orders${query(page)}`, {
        signal: deadline.signal,
      });
      if (!response.ok) {
        throw new Error(await describeFailure(response, 'Could not load orders'));
      }
      return decodeOrderPage(await response.json());
    } finally {
      deadline.settle();
    }
  }

  async place(input: NewOrder, signal?: AbortSignal): Promise<PlacedOrder> {
    const deadline = startDeadline(signal);
    try {
      const response = await fetch(`${this.baseUrl}/orders`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
        signal: deadline.signal,
      });
      if (!response.ok) {
        throw new Error(await describeFailure(response, 'Could not place the order'));
      }
      return decodePlacedOrder(await response.json());
    } finally {
      deadline.settle();
    }
  }
}

function query(page?: PageRequest): string {
  const asked = new URLSearchParams();

  if (page?.limit !== undefined) {
    asked.set('limit', String(page.limit));
  }

  if (page?.cursor !== undefined) {
    asked.set('cursor', page.cursor);
  }

  return asked.size === 0 ? '' : `?${asked.toString()}`;
}

interface Deadline {
  signal: AbortSignal;
  settle: () => void;
}

// Built from AbortController rather than AbortSignal.any, which jsdom lacks and older browsers do not have.
function startDeadline(caller?: AbortSignal): Deadline {
  const controller = new AbortController();
  const timer = setTimeout(
    () => controller.abort(new Error(`The API did not answer within ${String(REQUEST_TIMEOUT_MS)}ms`)),
    REQUEST_TIMEOUT_MS,
  );

  const forward = () => controller.abort(caller?.reason);
  caller?.addEventListener('abort', forward);

  return {
    signal: controller.signal,
    settle: () => {
      clearTimeout(timer);
      caller?.removeEventListener('abort', forward);
    },
  };
}

async function describeFailure(response: Response, fallback: string): Promise<string> {
  const body: unknown = await response.json().catch(() => null);
  return decodeFailureMessage(body) ?? `${fallback} (${response.status})`;
}
