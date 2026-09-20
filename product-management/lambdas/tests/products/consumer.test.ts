import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { SQSEvent } from 'aws-lambda';

// Secrets Manager is mocked: these tests exercise processing + failure behavior, not AWS.
vi.mock('../../src/products/shared/fetchSecret', () => ({
  fetchSecret: vi.fn(async () => JSON.stringify({ encryptionKey: 'test-key' })),
}));

import { handler } from '../../src/products/consumer';
import * as secret from '../../src/products/shared/fetchSecret';

function sqsEvent(...productIds: string[]): SQSEvent {
  return {
    Records: productIds.map((productId, i) => ({
      body: JSON.stringify({ productId }),
      messageId: `m${i}`,
    })),
  } as unknown as SQSEvent;
}

describe('processing consumer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    process.env.PROCESSING_DELAY_MS = '0'; // no artificial delay in tests
  });

  it('processes each message and reads the signing secret', async () => {
    await handler(sqsEvent('p1', 'p2'));
    expect(secret.fetchSecret).toHaveBeenCalledTimes(2);
  });

  it('throws (to trigger retry/DLQ) when the secret cannot be fetched', async () => {
    vi.mocked(secret.fetchSecret).mockRejectedValueOnce(new Error('secret missing'));
    await expect(handler(sqsEvent('p1'))).rejects.toThrow();
  });
});
