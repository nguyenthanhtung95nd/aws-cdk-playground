import { Task } from './model/task';
import { parseUpdate } from './shared/validator';
import { updateTask as updateTaskInDb } from './taskRepository';

export function updateTask(userId: string, id: string, body: string | undefined): Promise<Task> {
  const fields = parseUpdate(body);
  return updateTaskInDb(userId, id, fields);
}
