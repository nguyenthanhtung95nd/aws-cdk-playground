export const TASK_STATUSES = ['todo', 'doing', 'done'] as const;

export type TaskStatus = (typeof TASK_STATUSES)[number];

export interface Task {
  userId: string; // partition key - Cognito sub (the task owner)
  id: string; // sort key - taskId (uuid)
  title: string;
  status: TaskStatus;
  createdAt: string; // ISO 8601
  updatedAt: string; // ISO 8601
}

/** Fields a client may change on an existing task. Both are optional; at least one is required. */
export interface TaskUpdate {
  title?: string;
  status?: TaskStatus;
}
