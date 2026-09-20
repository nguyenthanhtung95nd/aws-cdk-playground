import { Task } from './model/task';
import { queryTasksByUser } from './taskRepository';

export function listTasks(userId: string): Promise<Task[]> {
  return queryTasksByUser(userId);
}
