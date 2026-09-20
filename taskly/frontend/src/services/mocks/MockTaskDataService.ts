import type { Task, TaskStatus } from '../../types/task';
import type { TaskGateway } from '../gateways';
import { ApiError } from '../errors';

const delay = (ms = 250) => new Promise((resolve) => setTimeout(resolve, ms));

function seed(): Task[] {
  const now = new Date().toISOString();
  return [
    { userId: 'mock', id: 'm1', title: 'Try the mock mode (npm run dev:mock)', status: 'doing', createdAt: now, updatedAt: now },
    { userId: 'mock', id: 'm2', title: 'Style the task list', status: 'todo', createdAt: now, updatedAt: now },
    { userId: 'mock', id: 'm3', title: 'Deploy the dev backend', status: 'done', createdAt: now, updatedAt: now },
  ];
}

/** In-memory task store for local mock mode. Mimics the API (latency, per-op behavior). */
export class MockTaskDataService implements TaskGateway {
  private tasks: Task[] = seed();
  private counter = 100;

  async list(): Promise<Task[]> {
    await delay();
    return [...this.tasks];
  }

  async create(title: string): Promise<Task> {
    await delay();
    const now = new Date().toISOString();
    const task: Task = { userId: 'mock', id: `m${++this.counter}`, title, status: 'todo', createdAt: now, updatedAt: now };
    this.tasks = [task, ...this.tasks];
    return task;
  }

  async update(id: string, fields: { title?: string; status?: TaskStatus }): Promise<Task> {
    await delay();
    const task = this.tasks.find((t) => t.id === id);
    if (!task) throw new ApiError('Task not found');
    Object.assign(task, fields, { updatedAt: new Date().toISOString() });
    return { ...task };
  }

  async remove(id: string): Promise<void> {
    await delay();
    this.tasks = this.tasks.filter((t) => t.id !== id);
  }
}
