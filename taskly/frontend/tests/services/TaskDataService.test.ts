import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TaskDataService } from '../../src/services/TaskDataService';
import { ApiError, SessionExpiredError } from '../../src/services/errors';

const auth = { getIdToken: vi.fn(async () => 'token-123') };
const svc = new TaskDataService('https://api.test', auth);

function mockFetch(status: number, body?: unknown) {
  global.fetch = vi.fn(async () => ({
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  })) as unknown as typeof fetch;
}

describe('TaskDataService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    auth.getIdToken.mockResolvedValue('token-123');
  });

  it('sends the Authorization header and returns the tasks', async () => {
    mockFetch(200, [{ id: 't1' }]);
    const tasks = await svc.list();
    expect(tasks).toEqual([{ id: 't1' }]);
    const init = vi.mocked(global.fetch).mock.calls[0][1] as RequestInit;
    expect((init.headers as Record<string, string>).Authorization).toBe('token-123');
  });

  it('maps a 401 response to SessionExpiredError', async () => {
    mockFetch(401, {});
    await expect(svc.list()).rejects.toBeInstanceOf(SessionExpiredError);
  });

  it('maps other errors to ApiError carrying the server message', async () => {
    mockFetch(400, { message: 'Title is required' });
    await expect(svc.create('')).rejects.toThrow('Title is required');
    await expect(svc.create('')).rejects.toBeInstanceOf(ApiError);
  });

  it('resolves to undefined on a 204 delete', async () => {
    mockFetch(204);
    await expect(svc.remove('t1')).resolves.toBeUndefined();
  });

  it('treats a missing token as an expired session', async () => {
    auth.getIdToken.mockRejectedValueOnce(new Error('no session'));
    mockFetch(200, []);
    await expect(svc.list()).rejects.toBeInstanceOf(SessionExpiredError);
  });
});
