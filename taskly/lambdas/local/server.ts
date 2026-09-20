/**
 * Local dev HTTP server for the Taskly backend. Reuses the real Lambda `handler` unchanged and
 * adapts plain HTTP <-> the API Gateway v2 event, talking to DynamoDB Local (Docker). No AWS.
 *
 *   npm run db:up   # start DynamoDB Local
 *   npm run dev     # this server on http://localhost:4000
 *
 * There is no Cognito locally, so the caller's identity comes from an `x-user-id` header
 * (default `local-dev-user`) - it stands in for the JWT `sub` claim the authorizer would provide.
 */
import { createServer, type IncomingMessage } from 'node:http';
import { CreateTableCommand, DescribeTableCommand, DynamoDBClient } from '@aws-sdk/client-dynamodb';

const PORT = 4000;
const DDB_ENDPOINT = 'http://localhost:8000';
const TABLE_NAME = 'taskly-local-tasks';

// Point the AWS SDK (including the handler's client) at DynamoDB Local, with dummy creds.
// Must be set BEFORE the handler module is imported (its client reads these at construction).
process.env.AWS_ENDPOINT_URL_DYNAMODB = DDB_ENDPOINT;
process.env.AWS_ACCESS_KEY_ID = 'local';
process.env.AWS_SECRET_ACCESS_KEY = 'local';
process.env.AWS_REGION = 'us-west-1';
process.env.TABLE_NAME = TABLE_NAME;

const CORS = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Headers': 'Content-Type,Authorization,x-user-id',
  'Access-Control-Allow-Methods': 'GET,POST,PUT,DELETE,OPTIONS',
};

async function ensureTable(): Promise<void> {
  const client = new DynamoDBClient({});
  try {
    await client.send(new DescribeTableCommand({ TableName: TABLE_NAME }));
  } catch {
    await client.send(
      new CreateTableCommand({
        TableName: TABLE_NAME,
        AttributeDefinitions: [
          { AttributeName: 'userId', AttributeType: 'S' },
          { AttributeName: 'id', AttributeType: 'S' },
        ],
        KeySchema: [
          { AttributeName: 'userId', KeyType: 'HASH' },
          { AttributeName: 'id', KeyType: 'RANGE' },
        ],
        BillingMode: 'PAY_PER_REQUEST',
      }),
    );
    console.log(`Created table ${TABLE_NAME}`);
  }
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
  // Import AFTER env is set so the handler's DynamoDB client uses the local endpoint.
  const { handler } = await import('../src/tasks/handler');

  const server = createServer(async (req, res) => {
    if (req.method === 'OPTIONS') {
      res.writeHead(204, CORS);
      res.end();
      return;
    }

    const url = new URL(req.url ?? '/', `http://localhost:${PORT}`);
    const parts = url.pathname.split('/').filter(Boolean); // e.g. ['tasks'] or ['tasks','abc']
    const id = parts[0] === 'tasks' ? parts[1] : undefined;
    const userId = (req.headers['x-user-id'] as string) || 'local-dev-user';
    const body = await readBody(req);

    const event = {
      body: body || undefined,
      pathParameters: id ? { id } : undefined,
      requestContext: {
        http: { method: req.method },
        authorizer: { jwt: { claims: { sub: userId } } },
      },
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
    console.log(`Taskly local API  ->  http://localhost:${PORT}   (DynamoDB Local at ${DDB_ENDPOINT})`);
    console.log(`Header 'x-user-id: <you>' picks the user (default: local-dev-user).`);
  });
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
