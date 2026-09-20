import type { Task, TaskStatus } from '../types/task';
import type { TaskGateway } from './gateways';
import { ApiError, SessionExpiredError } from './errors';

export interface TokenProvider {
  getIdToken(): Promise<string>;
}

/** Talks to the Taskly HTTP API. Maps 401 to SessionExpiredError so the UI can re-auth. */
export class TaskDataService implements TaskGateway {
  private readonly apiUrl: string;
  private readonly auth: TokenProvider;

  constructor(apiUrl: string, auth: TokenProvider) {
    this.apiUrl = apiUrl;
    this.auth = auth;
  }

  private async request<T>(path: string, init?: RequestInit): Promise<T> {
    let token: string;
    try {
      token = await this.auth.getIdToken();
    } catch {
      throw new SessionExpiredError('Session expired');
    }

    const res = await fetch(`${this.apiUrl}${path}`, {
      ...init,
      headers: { 'Content-Type': 'application/json', Authorization: token, ...init?.headers },
    });

    if (res.status === 401) {
      throw new SessionExpiredError('Session expired');
    }
    if (!res.ok) {
      const message = await res
        .json()
        .then((body: { message?: string }) => body.message ?? 'Request failed')
        .catch(() => 'Request failed');
      throw new ApiError(message);
    }
    if (res.status === 204) {
      return undefined as T;
    }
    return (await res.json()) as T;
  }

  list(): Promise<Task[]> {
    return this.request<Task[]>('/tasks');
  }

  create(title: string): Promise<Task> {
    return this.request<Task>('/tasks', { method: 'POST', body: JSON.stringify({ title }) });
  }

  update(id: string, fields: { title?: string; status?: TaskStatus }): Promise<Task> {
    return this.request<Task>(`/tasks/${id}`, { method: 'PUT', body: JSON.stringify(fields) });
  }

  remove(id: string): Promise<void> {
    return this.request<void>(`/tasks/${id}`, { method: 'DELETE' });
  }
}
