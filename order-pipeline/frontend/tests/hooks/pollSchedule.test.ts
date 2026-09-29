import { describe, expect, it } from 'vitest';
import {
  POLL_INTERVAL_MS,
  afterFailure,
  afterSuccess,
  nextDelay,
  startedActive,
  type PollRhythm,
} from '../../src/hooks/pollSchedule';
import type { Order, OrderStatus } from '../../src/types/order';

const NOW = 1_000_000;
const noJitter = () => 0.5;

function orderWith(status: OrderStatus): Order {
  return {
    orderId: 'a',
    customerName: 'Alice',
    product: 'Headphones',
    amount: 1499,
    notes: '',
    status,
    isHighValue: false,
    placedAt: '2026-09-24T08:00:00Z',
  };
}

function settledFor(ticks: number): PollRhythm {
  return { consecutiveFailures: 0, settledTicks: ticks, activeSince: null };
}

describe('nextDelay', () => {
  it('asks quickly while an order can still move', () => {
    expect(nextDelay(startedActive(NOW), NOW, noJitter)).toBe(POLL_INTERVAL_MS);
  });

  it('slows down once an order has been moving for a minute', () => {
    expect(nextDelay(startedActive(NOW), NOW + 60_000, noJitter)).toBe(10_000);
  });

  it.each([
    [0, 5_000],
    [1, 5_000],
    [2, 15_000],
    [3, 15_000],
  ])('eases off after %i settled looks', (ticks, expected) => {
    expect(nextDelay(settledFor(ticks), NOW, noJitter)).toBe(expected);
  });

  it('stops asking altogether once the rundown is over', () => {
    expect(nextDelay(settledFor(4), NOW, noJitter)).toBeNull();
  });

  it.each([
    [1, 2_000],
    [2, 4_000],
    [3, 8_000],
    [4, 16_000],
    [5, 30_000],
    [9, 30_000],
  ])('waits %ims longer after failure %i', (failures, expected) => {
    const rhythm: PollRhythm = { ...startedActive(NOW), consecutiveFailures: failures };

    expect(nextDelay(rhythm, NOW, noJitter)).toBe(expected);
  });

  it('spreads the wait so tabs opened together do not stay in step', () => {
    const rhythm = startedActive(NOW);

    expect(nextDelay(rhythm, NOW, () => 0)).toBe(1_600);
    expect(nextDelay(rhythm, NOW, () => 1)).toBe(2_400);
  });
});

describe('rhythm after a look', () => {
  it('keeps counting settled looks while nothing can move', () => {
    const once = afterSuccess([orderWith('COMPLETED')], startedActive(NOW), NOW);
    const twice = afterSuccess([orderWith('COMPLETED')], once, NOW);

    expect(twice.settledTicks).toBe(2);
    expect(twice.activeSince).toBeNull();
  });

  it('treats an empty list as nothing left to wait for', () => {
    expect(afterSuccess([], startedActive(NOW), NOW).activeSince).toBeNull();
  });

  it('starts the rundown over as soon as one order is moving again', () => {
    const settled = afterSuccess([orderWith('FAILED')], startedActive(NOW), NOW);

    const moving = afterSuccess([orderWith('PROCESSING')], settled, NOW);

    expect(moving.settledTicks).toBe(0);
    expect(moving.activeSince).toBe(NOW);
  });

  // The clock runs from when the order started moving, not from the last look, so a stuck order
  // reaches the slower rhythm instead of resetting the patience on every poll.
  it('keeps the moment an order started moving across later looks', () => {
    const first = afterSuccess([orderWith('PENDING')], startedActive(NOW), NOW);

    const later = afterSuccess([orderWith('PENDING')], first, NOW + 30_000);

    expect(later.activeSince).toBe(NOW);
  });

  it('forgets the failures once a look succeeds', () => {
    const struggling = afterFailure(afterFailure(startedActive(NOW)));

    expect(afterSuccess([orderWith('PENDING')], struggling, NOW).consecutiveFailures).toBe(0);
  });
});
