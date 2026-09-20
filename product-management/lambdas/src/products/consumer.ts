import { SQSEvent } from 'aws-lambda';
import { createHmac } from 'node:crypto';
import { fetchSecret } from './shared/fetchSecret';

// Simulated processing time per message (overridable via env for local runs and tests).
const DEFAULT_PROCESSING_DELAY_MS = 2000;
const SECRET_ID = process.env.PROCESSING_SECRET_ID as string;

/**
 * SQS consumer: for each "product created" message, simulate processing work, then sign the
 * productId with an HMAC keyed by a secret. An unhandled throw here lets SQS retry and, after
 * maxReceiveCount, route the message to the dead-letter queue.
 */
export async function handler(event: SQSEvent): Promise<void> {
  for (const record of event.Records) {
    const { productId } = JSON.parse(record.body) as { productId: string };
    console.log('Processing product', productId);

    await delay(Number(process.env.PROCESSING_DELAY_MS ?? DEFAULT_PROCESSING_DELAY_MS));

    const secretValue = await fetchSecret(SECRET_ID);
    const { encryptionKey } = JSON.parse(secretValue) as { encryptionKey: string };
    const signature = createHmac('sha256', encryptionKey).update(productId).digest('hex');

    console.log('Processed product', productId, 'signature', signature);
  }
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
