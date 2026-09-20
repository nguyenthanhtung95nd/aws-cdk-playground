import { randomUUID } from 'node:crypto';
import { Task } from './model/task';
import { parseJsonBody, parseTitle } from './shared/validator';
import { putTask } from './taskRepository';

export async function createTask(userId: string, body: string | undefined): Promise<Task> {
  const input = parseJsonBody(body);
  const title = parseTitle(input.title);
  const now = new Date().toISOString();

  const task: Task = {
    userId,
    id: randomUUID(),
    title,
    status: 'todo', // new tasks always start in `todo`
    createdAt: now,
    updatedAt: now,
  };

  await putTask(task);
  return task;
}
