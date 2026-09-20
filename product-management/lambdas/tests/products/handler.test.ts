import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { APIGatewayProxyEventV2 } from 'aws-lambda';

// S3 + DynamoDB are mocked: these tests exercise routing + validation, not AWS.
vi.mock('../../src/products/productRepository', () => ({
  putProduct: vi.fn(async () => {}),
  scanProducts: vi.fn(async () => []),
  getProduct: vi.fn(async () => undefined),
  deleteProduct: vi.fn(async () => {}),
}));
vi.mock('../../src/products/imageStore', () => ({
  putProductImage: vi.fn(async () => 'https://bucket.s3.us-west-1.amazonaws.com/products/x.png'),
  deleteProductImage: vi.fn(async () => {}),
}));
vi.mock('../../src/products/queueClient', () => ({
  enqueueProductCreated: vi.fn(async () => {}),
}));

import { handler } from '../../src/products/handler';
import * as repo from '../../src/products/productRepository';
import * as imageStore from '../../src/products/imageStore';
import * as queue from '../../src/products/queueClient';

const VALID_IMAGE = 'data:image/png;base64,aGVsbG8=';

function event(method: string, opts: { body?: string; id?: string } = {}): APIGatewayProxyEventV2 {
  return {
    body: opts.body,
    pathParameters: opts.id ? { id: opts.id } : undefined,
    requestContext: { http: { method } },
  } as unknown as APIGatewayProxyEventV2;
}

describe('products handler', () => {
  beforeEach(() => vi.clearAllMocks());

  it('GET returns 200 with the catalog', async () => {
    vi.mocked(repo.scanProducts).mockResolvedValueOnce([
      { id: 'p1', name: 'x', description: 'd', price: 1, imageUrl: 'u', createdAt: '', updatedAt: '' },
    ]);
    const res = await handler(event('GET'));
    expect(res.statusCode).toBe(200);
    expect(repo.scanProducts).toHaveBeenCalledOnce();
    expect(JSON.parse(res.body as string)).toHaveLength(1);
  });

  it('POST creates a product and returns 201', async () => {
    const body = JSON.stringify({ name: 'Shirt', description: 'nice', price: 10, imageData: VALID_IMAGE });
    const res = await handler(event('POST', { body }));
    expect(res.statusCode).toBe(201);
    expect(imageStore.putProductImage).toHaveBeenCalledOnce();
    expect(repo.putProduct).toHaveBeenCalledOnce();
    const product = JSON.parse(res.body as string);
    expect(product).toMatchObject({ name: 'Shirt', description: 'nice', price: 10 });
    expect(product.id).toBeTruthy();
    expect(queue.enqueueProductCreated).toHaveBeenCalledOnce();
  });

  it('POST still returns 201 when enqueue fails (best-effort)', async () => {
    vi.mocked(queue.enqueueProductCreated).mockRejectedValueOnce(new Error('sqs down'));
    const body = JSON.stringify({ name: 'Shirt', description: 'nice', price: 10, imageData: VALID_IMAGE });
    const res = await handler(event('POST', { body }));
    expect(res.statusCode).toBe(201);
    expect(repo.putProduct).toHaveBeenCalledOnce();
  });

  it('POST with a missing field returns 400 and no side effects', async () => {
    const body = JSON.stringify({ name: 'Shirt', price: 10, imageData: VALID_IMAGE });
    const res = await handler(event('POST', { body }));
    expect(res.statusCode).toBe(400);
    expect(imageStore.putProductImage).not.toHaveBeenCalled();
    expect(repo.putProduct).not.toHaveBeenCalled();
  });

  it('POST with a non-image imageData returns 400', async () => {
    const body = JSON.stringify({ name: 'Shirt', description: 'd', price: 1, imageData: 'not-a-data-uri' });
    const res = await handler(event('POST', { body }));
    expect(res.statusCode).toBe(400);
    expect(repo.putProduct).not.toHaveBeenCalled();
  });

  it('DELETE removes an existing product and returns 200', async () => {
    vi.mocked(repo.getProduct).mockResolvedValueOnce({
      id: 'p1', name: 'x', description: 'd', price: 1, imageUrl: 'https://b/products/p1.png', createdAt: '', updatedAt: '',
    });
    const res = await handler(event('DELETE', { id: 'p1' }));
    expect(res.statusCode).toBe(200);
    expect(repo.deleteProduct).toHaveBeenCalledWith('p1');
  });

  it('DELETE on a missing product returns 404', async () => {
    vi.mocked(repo.getProduct).mockResolvedValueOnce(undefined);
    const res = await handler(event('DELETE', { id: 'p9' }));
    expect(res.statusCode).toBe(404);
    expect(repo.deleteProduct).not.toHaveBeenCalled();
  });

  it('DELETE without an id returns 400', async () => {
    const res = await handler(event('DELETE'));
    expect(res.statusCode).toBe(400);
  });

  it('returns 405 for an unsupported method', async () => {
    const res = await handler(event('PUT', { id: 'p1' }));
    expect(res.statusCode).toBe(405);
  });
});
