/**
 * Local dev HTTP server for the product-management backend. Reuses the real Lambda `handler`
 * unchanged and adapts plain HTTP <-> the API Gateway v2 event, talking to LocalStack (Docker)
 * for S3 / DynamoDB. No real AWS.
 *
 *   npm run aws:up   # start LocalStack
 *   npm run dev      # this server on http://localhost:4000
 *
 * There is no auth (the API is open), so no identity header is needed.
 * SQS/Secrets processing is driven separately by local/worker.ts.
 */
import { createServer, type IncomingMessage } from 'node:http';
import {
  CreateTableCommand,
  DescribeTableCommand,
  DynamoDBClient,
} from '@aws-sdk/client-dynamodb';
import { CreateBucketCommand, HeadBucketCommand, S3Client } from '@aws-sdk/client-s3';
import { CreateQueueCommand, SQSClient } from '@aws-sdk/client-sqs';
import { LOCALSTACK_ENDPOINT, QUEUE_NAME, seedLocalEnv } from './localEnv';

const PORT = 4000;
const TABLE_NAME = 'productmgmt-local-products';
const IMAGES_BUCKET = 'productmgmt-local-product-images';

// Point the AWS SDK v3 (including the handler's clients) at LocalStack, with dummy creds.
// Must be set BEFORE the handler module is imported (its clients read these at construction).
seedLocalEnv();
process.env.PRODUCTS_TABLE_NAME = TABLE_NAME;
process.env.PRODUCT_IMAGES_BUCKET_NAME = IMAGES_BUCKET;

const CORS = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Headers': 'Content-Type,Authorization',
  'Access-Control-Allow-Methods': 'GET,POST,DELETE,OPTIONS',
};

async function ensureTable(): Promise<void> {
  const client = new DynamoDBClient({});
  try {
    await client.send(new DescribeTableCommand({ TableName: TABLE_NAME }));
  } catch {
    await client.send(
      new CreateTableCommand({
        TableName: TABLE_NAME,
        AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
        KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
        BillingMode: 'PAY_PER_REQUEST',
      }),
    );
    console.log(`Created table ${TABLE_NAME}`);
  }
}

async function ensureBucket(): Promise<void> {
  const client = new S3Client({ forcePathStyle: true });
  try {
    await client.send(new HeadBucketCommand({ Bucket: IMAGES_BUCKET }));
  } catch {
    await client.send(new CreateBucketCommand({ Bucket: IMAGES_BUCKET }));
    console.log(`Created bucket ${IMAGES_BUCKET}`);
  }
}

/** CreateQueue is idempotent - returns the URL whether or not the queue already exists. */
async function ensureQueueUrl(): Promise<string> {
  const client = new SQSClient({});
  const result = await client.send(new CreateQueueCommand({ QueueName: QUEUE_NAME }));
  return result.QueueUrl as string;
}

function readBody(req: IncomingMessage): Promise<string> {
  return new Promise((resolve) => {
    let data = '';
    req.on('data', (chunk) => (data += chunk));
    req.on('end', () => resolve(data));
  });
}

async function main(): Promise<void> {
  await ensureTable();
  await ensureBucket();
  // Enqueue target for created products; set BEFORE importing the handler (queueClient reads it).
  process.env.PRODUCTS_QUEUE_URL = await ensureQueueUrl();
  // Import AFTER env is set so the handler's clients use the local endpoint + resource names.
  const { handler } = await import('../src/products/handler');

  const server = createServer(async (req, res) => {
    if (req.method === 'OPTIONS') {
      res.writeHead(204, CORS);
      res.end();
      return;
    }

    const url = new URL(req.url ?? '/', `http://localhost:${PORT}`);
    const parts = url.pathname.split('/').filter(Boolean); // ['products'] or ['products','abc']
    const id = parts[0] === 'products' ? parts[1] : undefined;
    const body = await readBody(req);

    const event = {
      body: body || undefined,
      rawPath: url.pathname,
      pathParameters: id ? { id } : undefined,
      requestContext: { http: { method: req.method } },
    };

    try {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const result = await handler(event as any);
      res.writeHead(result.statusCode ?? 200, { ...CORS, ...(result.headers ?? {}) });
      res.end(typeof result.body === 'string' ? result.body : '');
    } catch (error) {
      console.error(error);
      res.writeHead(500, { ...CORS, 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ message: 'Local server error' }));
    }
  });

  server.listen(PORT, () => {
    console.log(`product-management local API  ->  http://localhost:${PORT}   (LocalStack ${LOCALSTACK_ENDPOINT})`);
  });
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
