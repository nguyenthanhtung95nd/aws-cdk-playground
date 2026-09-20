/**
 * Local SQS worker for offline processing. LocalStack has a real queue but does not invoke a
 * Lambda from an event-source mapping, so this polls the queue and calls the real consumer
 * `handler` unchanged. Failed messages are left on the queue; after maxReceiveCount they redrive
 * to the DLQ, mirroring production.
 *
 *   npm run aws:up   # start LocalStack
 *   npm run worker   # this poller
 */
import {
  SQSClient,
  CreateQueueCommand,
  GetQueueAttributesCommand,
  ReceiveMessageCommand,
  DeleteMessageCommand,
} from '@aws-sdk/client-sqs';
import {
  SecretsManagerClient,
  CreateSecretCommand,
  PutSecretValueCommand,
  DescribeSecretCommand,
} from '@aws-sdk/client-secrets-manager';
import { seedLocalEnv, QUEUE_NAME, DLQ_NAME, SECRET_ID } from './localEnv';

seedLocalEnv();
process.env.PROCESSING_SECRET_ID = SECRET_ID;
process.env.PROCESSING_DELAY_MS = '500'; // shorter than production for a snappier local loop

async function ensureSecret(): Promise<void> {
  const client = new SecretsManagerClient({});
  const secretString = JSON.stringify({ encryptionKey: 'local-dev-key' });
  try {
    await client.send(new DescribeSecretCommand({ SecretId: SECRET_ID }));
    await client.send(new PutSecretValueCommand({ SecretId: SECRET_ID, SecretString: secretString }));
  } catch {
    await client.send(new CreateSecretCommand({ Name: SECRET_ID, SecretString: secretString }));
    console.log(`Created secret ${SECRET_ID}`);
  }
}

async function ensureQueues(sqs: SQSClient): Promise<string> {
  const dlq = await sqs.send(new CreateQueueCommand({ QueueName: DLQ_NAME }));
  const attrs = await sqs.send(
    new GetQueueAttributesCommand({ QueueUrl: dlq.QueueUrl, AttributeNames: ['QueueArn'] }),
  );
  const dlqArn = attrs.Attributes?.QueueArn;
  const main = await sqs.send(
    new CreateQueueCommand({
      QueueName: QUEUE_NAME,
      Attributes: {
        // Short visibility so failed messages redrive quickly enough to watch locally.
        VisibilityTimeout: '5',
        RedrivePolicy: JSON.stringify({ deadLetterTargetArn: dlqArn, maxReceiveCount: 3 }),
      },
    }),
  );
  return main.QueueUrl as string;
}

async function main(): Promise<void> {
  await ensureSecret();
  const sqs = new SQSClient({});
  const queueUrl = await ensureQueues(sqs);
  const { handler } = await import('../src/products/consumer');
  console.log(`Worker polling ${queueUrl} (Ctrl+C to stop)`);

  for (;;) {
    const received = await sqs.send(
      new ReceiveMessageCommand({ QueueUrl: queueUrl, MaxNumberOfMessages: 10, WaitTimeSeconds: 5 }),
    );
    for (const message of received.Messages ?? []) {
      const event = { Records: [{ body: message.Body ?? '', messageId: message.MessageId ?? '' }] };
      try {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        await handler(event as any);
        await sqs.send(
          new DeleteMessageCommand({ QueueUrl: queueUrl, ReceiptHandle: message.ReceiptHandle as string }),
        );
      } catch (error) {
        console.error('Processing failed; leaving message for retry/DLQ', error);
      }
    }
  }
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
