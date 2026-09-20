import { TASK_STATUSES, TaskStatus, TaskUpdate } from '../model/task';
import { ValidationError } from './errors';

const TITLE_MAX_LENGTH = 200;

/** Parses a request body into an object; malformed JSON is a client error, not a crash. */
export function parseJsonBody(body: string | undefined): Record<string, unknown> {
  if (!body) return {};
  try {
    return JSON.parse(body) as Record<string, unknown>;
  } catch {
    throw new ValidationError('Invalid request body');
  }
}

export function parseTitle(raw: unknown): string {
  if (typeof raw !== 'string' || raw.trim().length === 0) {
    throw new ValidationError('Title is required');
  }
  const title = raw.trim();
  if (title.length > TITLE_MAX_LENGTH) {
    throw new ValidationError(`Title must be 1-${TITLE_MAX_LENGTH} characters`);
  }
  return title;
}

export function parseStatus(raw: unknown): TaskStatus {
  if (typeof raw !== 'string' || !TASK_STATUSES.includes(raw as TaskStatus)) {
    throw new ValidationError(`Invalid status; expected one of: ${TASK_STATUSES.join(', ')}`);
  }
  return raw as TaskStatus;
}

/** Parses a partial update. Only provided fields are validated; at least one is required. */
export function parseUpdate(body: string | undefined): TaskUpdate {
  const input = parseJsonBody(body);
  const update: TaskUpdate = {};
  if (input.title !== undefined) update.title = parseTitle(input.title);
  if (input.status !== undefined) update.status = parseStatus(input.status);
  if (update.title === undefined && update.status === undefined) {
    throw new ValidationError('Provide a title or status to update');
  }
  return update;
}
