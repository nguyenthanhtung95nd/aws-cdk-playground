import { SQSClient, SendMessageCommand } from '@aws-sdk/client-sqs';

// Client is created once per container so connections are reused across warm invocations.
// SDK v3 honors AWS_ENDPOINT_URL, so LocalStack needs no code change.
const client = new SQSClient({});
const QUEUE_URL = process.env.PRODUCTS_QUEUE_URL as string;

/** Enqueues a "product created" message for asynchronous processing. */
export async function enqueueProductCreated(productId: string): Promise<void> {
  await client.send(
    new SendMessageCommand({ QueueUrl: QUEUE_URL, MessageBody: JSON.stringify({ productId }) }),
  );
}
