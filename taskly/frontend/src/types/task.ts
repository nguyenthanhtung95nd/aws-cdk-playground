export const TASK_STATUSES = ['todo', 'doing', 'done'] as const;

export type TaskStatus = (typeof TASK_STATUSES)[number];

export interface Task {
  userId: string;
  id: string;
  title: string;
  status: TaskStatus;
  createdAt: string;
  updatedAt: string;
}

export const STATUS_LABEL: Record<TaskStatus, string> = {
  todo: 'To do',
  doing: 'Doing',
  done: 'Done',
};

/** Advances a task through its lifecycle: todo -> doing -> done -> todo. */
export function nextStatus(current: TaskStatus): TaskStatus {
  const order = TASK_STATUSES;
  return order[(order.indexOf(current) + 1) % order.length];
}
