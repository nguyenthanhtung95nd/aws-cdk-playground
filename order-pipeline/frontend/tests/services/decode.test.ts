import { describe, expect, it } from 'vitest';
import { decodeFailureMessage, decodeOrderPage, decodePlacedOrder } from '../../src/services/decode';

const validOrder = {
  orderId: 'a',
  customerName: 'Alice',
  product: 'Headphones',
  amount: 1499,
  notes: '',
  status: 'PENDING',
  isHighValue: false,
  placedAt: '2026-09-24T08:00:00Z',
};

describe('decodeOrderPage', () => {
  it('accepts a well formed list', () => {
    expect(decodeOrderPage({ orders: [validOrder] }).orders).toHaveLength(1);
  });

  it('refuses a body that is not a list', () => {
    expect(() => decodeOrderPage([validOrder])).toThrow('not a page of orders');
  });

  it.each([
    ['orderId', { ...validOrder, orderId: 42 }],
    ['amount as text', { ...validOrder, amount: '1499' }],
    ['unknown status', { ...validOrder, status: 'SHIPPED' }],
    ['missing isHighValue', { ...validOrder, isHighValue: undefined }],
    ['null instead of an order', null],
  ])('refuses an order with a bad %s', (_label, broken) => {
    expect(() => decodeOrderPage({ orders: [broken] })).toThrow('shape this app does not understand');
  });

  // The api drops the field altogether when there is nothing more, so its absence has to read as
  // an answer rather than as a body the app cannot understand.
  it('reads a missing cursor as nothing more to fetch', () => {
    expect(decodeOrderPage({ orders: [] }).nextCursor).toBeNull();
  });

  it('keeps the cursor the api handed out', () => {
    expect(decodeOrderPage({ orders: [], nextCursor: 'more-please' }).nextCursor).toBe('more-please');
  });

  it('refuses a cursor that is not text', () => {
    expect(() => decodeOrderPage({ orders: [], nextCursor: 7 })).toThrow(
      'shape this app does not understand',
    );
  });
});

describe('decodePlacedOrder', () => {
  it('accepts the placement response', () => {
    expect(decodePlacedOrder({ orderId: 'a', status: 'PENDING' })).toEqual({
      orderId: 'a',
      status: 'PENDING',
    });
  });

  it('refuses a response without a status', () => {
    expect(() => decodePlacedOrder({ orderId: 'a' })).toThrow();
  });
});

describe('decodeFailureMessage', () => {
  it('reads the api message when there is one', () => {
    expect(decodeFailureMessage({ message: 'customerName is required.' })).toBe(
      'customerName is required.',
    );
  });

  it.each([null, 'plain text', { message: 42 }])('returns null for %s', (body) => {
    expect(decodeFailureMessage(body)).toBeNull();
  });
});
