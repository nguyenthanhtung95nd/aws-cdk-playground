import { deleteTask as deleteTaskInDb } from './taskRepository';

export function deleteTask(userId: string, id: string): Promise<void> {
  return deleteTaskInDb(userId, id);
}
