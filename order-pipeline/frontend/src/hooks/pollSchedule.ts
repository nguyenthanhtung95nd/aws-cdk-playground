import { isTerminal, type Order } from '../types/order';

export const POLL_INTERVAL_MS = 2000;

const SLOW_ACTIVE_MS = 10_000;
const ACTIVE_PATIENCE_MS = 60_000;
const SETTLED_STEPS = [5_000, 5_000, 15_000, 15_000];
const FAILURE_CEILING_MS = 30_000;
const JITTER_SPREAD = 0.2;

export interface PollRhythm {
  consecutiveFailures: number;
  settledTicks: number;
  activeSince: number | null;
}

export function startedActive(now: number): PollRhythm {
  return { consecutiveFailures: 0, settledTicks: 0, activeSince: now };
}

export function afterSuccess(orders: Order[], previous: PollRhythm, now: number): PollRhythm {
  if (orders.some((order) => !isTerminal(order.status))) {
    return { consecutiveFailures: 0, settledTicks: 0, activeSince: previous.activeSince ?? now };
  }

  return { consecutiveFailures: 0, settledTicks: previous.settledTicks + 1, activeSince: null };
}

export function afterFailure(previous: PollRhythm): PollRhythm {
  return { ...previous, consecutiveFailures: previous.consecutiveFailures + 1 };
}

// null means stop asking. Once every order has settled, nothing can change without somebody doing
// something, and that somebody will tell us when they do.
export function nextDelay(
  rhythm: PollRhythm,
  now: number,
  random: () => number = Math.random,
): number | null {
  const base = baseDelay(rhythm, now);
  return base === null ? null : jittered(base, random);
}

function baseDelay(rhythm: PollRhythm, now: number): number | null {
  if (rhythm.consecutiveFailures > 0) {
    return Math.min(POLL_INTERVAL_MS * 2 ** (rhythm.consecutiveFailures - 1), FAILURE_CEILING_MS);
  }

  if (rhythm.activeSince !== null) {
    // One order stuck part-way would otherwise pin the tab at half a request a second for as long
    // as it stays open.
    return now - rhythm.activeSince >= ACTIVE_PATIENCE_MS ? SLOW_ACTIVE_MS : POLL_INTERVAL_MS;
  }

  return SETTLED_STEPS[rhythm.settledTicks] ?? null;
}

// Tabs opened together would otherwise stay in step and ask at the same instant for as long as
// they are all open.
function jittered(base: number, random: () => number): number {
  return Math.round(base * (1 + (random() * 2 - 1) * JITTER_SPREAD));
}
