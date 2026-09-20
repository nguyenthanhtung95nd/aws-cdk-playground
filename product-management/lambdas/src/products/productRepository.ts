import { DynamoDBClient } from '@aws-sdk/client-dynamodb';
import {
  DeleteCommand,
  DynamoDBDocumentClient,
  GetCommand,
  PutCommand,
  ScanCommand,
} from '@aws-sdk/lib-dynamodb';
import { Product } from './model/product';

// Clients are created once per container (outside the handler) so connections are reused
// across warm invocations. SDK v3 honors AWS_ENDPOINT_URL, so LocalStack needs no code change.
const client = new DynamoDBClient({});
const docClient = DynamoDBDocumentClient.from(client);
const TABLE_NAME = process.env.PRODUCTS_TABLE_NAME as string;

export async function putProduct(product: Product): Promise<void> {
  await docClient.send(new PutCommand({ TableName: TABLE_NAME, Item: product }));
}

/** Returns the whole catalog, newest first. Scan is acceptable for a small shared catalog. */
export async function scanProducts(): Promise<Product[]> {
  const result = await docClient.send(new ScanCommand({ TableName: TABLE_NAME }));
  const products = (result.Items as Product[] | undefined) ?? [];
  products.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
  return products;
}

export async function getProduct(id: string): Promise<Product | undefined> {
  const result = await docClient.send(new GetCommand({ TableName: TABLE_NAME, Key: { id } }));
  return result.Item as Product | undefined;
}

export async function deleteProduct(id: string): Promise<void> {
  await docClient.send(new DeleteCommand({ TableName: TABLE_NAME, Key: { id } }));
}
