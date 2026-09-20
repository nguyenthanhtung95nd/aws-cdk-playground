import {
  DeleteItemCommand,
  DynamoDBClient,
  PutItemCommand,
  QueryCommand,
  UpdateItemCommand,
} from '@aws-sdk/client-dynamodb';
import { marshall, unmarshall } from '@aws-sdk/util-dynamodb';
import { Task, TaskUpdate } from './model/task';
import { NotFoundError } from './shared/errors';

// Client is created once per container (outside the handler) so connections are reused
// across warm invocations.
const client = new DynamoDBClient({});
const TABLE_NAME = process.env.TABLE_NAME as string;

// A failed `attribute_exists(id)` guard means the item isn't there (wrong id, or another
// user's task) - surface it as a 404 rather than leaking the DynamoDB exception.
function mapMissingItemToNotFound(error: unknown, id: string): never {
  if ((error as Error).name === 'ConditionalCheckFailedException') {
    throw new NotFoundError(`Task ${id} not found`);
  }
  throw error;
}

export async function putTask(task: Task): Promise<void> {
  await client.send(
    new PutItemCommand({
      TableName: TABLE_NAME,
      Item: marshall(task),
    }),
  );
}

export async function queryTasksByUser(userId: string): Promise<Task[]> {
  const result = await client.send(
    new QueryCommand({
      TableName: TABLE_NAME,
      KeyConditionExpression: 'userId = :userId',
      ExpressionAttributeValues: marshall({ ':userId': userId }),
    }),
  );
  return (result.Items ?? []).map((item) => unmarshall(item) as Task);
}

export async function updateTask(userId: string, id: string, fields: TaskUpdate): Promise<Task> {
  // Build the SET clause only from the fields the client sent; `updatedAt` always changes.
  // Every attribute goes through ExpressionAttributeNames so reserved words (e.g. status) are safe.
  const names: Record<string, string> = { '#updatedAt': 'updatedAt' };
  const values: Record<string, string> = { ':updatedAt': new Date().toISOString() };
  const sets: string[] = ['#updatedAt = :updatedAt'];

  if (fields.title !== undefined) {
    names['#title'] = 'title';
    values[':title'] = fields.title;
    sets.push('#title = :title');
  }
  if (fields.status !== undefined) {
    names['#status'] = 'status';
    values[':status'] = fields.status;
    sets.push('#status = :status');
  }

  try {
    const result = await client.send(
      new UpdateItemCommand({
        TableName: TABLE_NAME,
        Key: marshall({ userId, id }),
        UpdateExpression: `SET ${sets.join(', ')}`,
        ExpressionAttributeNames: names,
        ExpressionAttributeValues: marshall(values),
        // Guard turns "task of another user" or "wrong id" into a clean 404 instead of a
        // silent upsert - the item must already exist for this userId.
        ConditionExpression: 'attribute_exists(id)',
        ReturnValues: 'ALL_NEW',
      }),
    );
    return unmarshall(result.Attributes ?? {}) as Task;
  } catch (error) {
    mapMissingItemToNotFound(error, id);
  }
}

export async function deleteTask(userId: string, id: string): Promise<void> {
  try {
    await client.send(
      new DeleteItemCommand({
        TableName: TABLE_NAME,
        Key: marshall({ userId, id }),
        ConditionExpression: 'attribute_exists(id)',
      }),
    );
  } catch (error) {
    mapMissingItemToNotFound(error, id);
  }
}
