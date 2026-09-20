import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { APIGatewayProxyEventV2WithJWTAuthorizer } from 'aws-lambda';
import { NotFoundError } from '../../src/tasks/shared/errors';

// DynamoDB is mocked: these tests exercise routing + validation, not AWS.
vi.mock('../../src/tasks/taskRepository', () => ({
  putTask: vi.fn(async () => {}),
  queryTasksByUser: vi.fn(async () => []),
  updateTask: vi.fn(async (userId: string, id: string, fields: { title?: string; status?: string }) => ({
    userId,
    id,
    title: fields.title ?? 'old',
    status: fields.status ?? 'todo',
    createdAt: '',
    updatedAt: 'now',
  })),
  deleteTask: vi.fn(async () => {}),
}));

import { handler } from '../../src/tasks/handler';
import * as repo from '../../src/tasks/taskRepository';

function event(
  method: string,
  opts: { sub?: string; body?: string; id?: string } = {},
): APIGatewayProxyEventV2WithJWTAuthorizer {
  return {
    body: opts.body,
    pathParameters: opts.id ? { id: opts.id } : undefined,
    requestContext: {
      http: { method },
      authorizer: opts.sub === undefined ? {} : { jwt: { claims: { sub: opts.sub } } },
    },
  } as unknown as APIGatewayProxyEventV2WithJWTAuthorizer;
}

describe('tasks handler', () => {
  beforeEach(() => vi.clearAllMocks());

  it('returns 401 when the token carries no user id', async () => {
    const res = await handler(event('GET'));
    expect(res.statusCode).toBe(401);
  });

  it('GET returns 200 with the caller tasks', async () => {
    vi.mocked(repo.queryTasksByUser).mockResolvedValueOnce([
      { userId: 'u1', id: 't1', title: 'x', status: 'todo', createdAt: '', updatedAt: '' },
    ]);
    const res = await handler(event('GET', { sub: 'u1' }));
    expect(res.statusCode).toBe(200);
    expect(repo.queryTasksByUser).toHaveBeenCalledWith('u1');
  });

  it('POST creates a task scoped to the caller and returns 201', async () => {
    const res = await handler(event('POST', { sub: 'u1', body: JSON.stringify({ title: 'Buy milk' }) }));
    expect(res.statusCode).toBe(201);
    expect(repo.putTask).toHaveBeenCalledOnce();
    const body = JSON.parse(res.body as string);
    expect(body).toMatchObject({ userId: 'u1', title: 'Buy milk', status: 'todo' });
  });

  it('POST with a blank title returns 400', async () => {
    const res = await handler(event('POST', { sub: 'u1', body: JSON.stringify({ title: '   ' }) }));
    expect(res.statusCode).toBe(400);
  });

  it('PUT updates status and returns 200', async () => {
    const res = await handler(event('PUT', { sub: 'u1', id: 't1', body: JSON.stringify({ status: 'done' }) }));
    expect(res.statusCode).toBe(200);
    expect(repo.updateTask).toHaveBeenCalledWith('u1', 't1', { status: 'done' });
    expect(JSON.parse(res.body as string).status).toBe('done');
  });

  it('PUT with an invalid status returns 400', async () => {
    const res = await handler(event('PUT', { sub: 'u1', id: 't1', body: JSON.stringify({ status: 'nope' }) }));
    expect(res.statusCode).toBe(400);
    expect(repo.updateTask).not.toHaveBeenCalled();
  });

  it('PUT on a missing task returns 404', async () => {
    vi.mocked(repo.updateTask).mockRejectedValueOnce(new NotFoundError('Task t9 not found'));
    const res = await handler(event('PUT', { sub: 'u1', id: 't9', body: JSON.stringify({ title: 'x' }) }));
    expect(res.statusCode).toBe(404);
  });

  it('DELETE returns 204 and no body', async () => {
    const res = await handler(event('DELETE', { sub: 'u1', id: 't1' }));
    expect(res.statusCode).toBe(204);
    expect(res.body).toBeUndefined();
    expect(repo.deleteTask).toHaveBeenCalledWith('u1', 't1');
  });

  it('DELETE on a missing task returns 404', async () => {
    vi.mocked(repo.deleteTask).mockRejectedValueOnce(new NotFoundError('Task t9 not found'));
    const res = await handler(event('DELETE', { sub: 'u1', id: 't9' }));
    expect(res.statusCode).toBe(404);
  });

  it('returns 405 for an unsupported method', async () => {
    const res = await handler(event('PATCH', { sub: 'u1' }));
    expect(res.statusCode).toBe(405);
  });
});
