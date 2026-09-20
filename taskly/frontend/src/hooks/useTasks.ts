import { useCallback, useEffect, useState } from 'react';
import type { Task } from '../types/task';
import { nextStatus } from '../types/task';
import type { TaskGateway } from '../services/gateways';
import { SessionExpiredError } from '../services/errors';

export type LoadStatus = 'loading' | 'ready' | 'error';

export function useTasks(data: TaskGateway, onExpired: () => void) {
  const [tasks, setTasks] = useState<Task[]>([]);
  const [status, setStatus] = useState<LoadStatus>('loading');

  // Session-expiry is handled the same way everywhere: surface it to the app, don't treat it
  // as a normal error. Returns true when it swallowed the error. `onExpired` must be stable
  // (memoized by the caller) or the effect below would re-run every render.
  const handledExpiry = useCallback(
    (error: unknown): boolean => {
      if (error instanceof SessionExpiredError) {
        onExpired();
        return true;
      }
      return false;
    },
    [onExpired],
  );

  const reload = useCallback(async () => {
    setStatus('loading');
    try {
      setTasks(await data.list());
      setStatus('ready');
    } catch (error) {
      if (!handledExpiry(error)) setStatus('error');
    }
  }, [data, handledExpiry]);

  useEffect(() => {
    // Initial load on mount; reload is memoized so this runs once per service.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    reload();
  }, [reload]);

  const create = useCallback(
    async (title: string) => {
      try {
        const created = await data.create(title);
        setTasks((prev) => [created, ...prev]);
      } catch (error) {
        if (handledExpiry(error)) return; // expiry -> banner; nothing for the form to show
        throw error; // other errors -> let CreateTaskForm surface them
      }
    },
    [data, handledExpiry],
  );

  // Optimistic single-task mutation: apply locally, roll that one task back on failure.
  // Rollback uses the functional updater so it never closes over a stale `tasks` snapshot.
  const mutate = useCallback(
    async (task: Task, optimistic: (t: Task) => Task, apply: () => Promise<unknown>) => {
      setTasks((prev) => prev.map((t) => (t.id === task.id ? optimistic(t) : t)));
      try {
        await apply();
      } catch (error) {
        setTasks((prev) => prev.map((t) => (t.id === task.id ? task : t)));
        handledExpiry(error);
      }
    },
    [handledExpiry],
  );

  const changeStatus = useCallback(
    (task: Task) => {
      const next = nextStatus(task.status);
      return mutate(task, (t) => ({ ...t, status: next }), () => data.update(task.id, { status: next }));
    },
    [data, mutate],
  );

  const rename = useCallback(
    (task: Task, title: string) =>
      mutate(task, (t) => ({ ...t, title }), () => data.update(task.id, { title })),
    [data, mutate],
  );

  const remove = useCallback(
    async (task: Task) => {
      let snapshot: Task[] = [];
      setTasks((prev) => {
        snapshot = prev; // capture inside the updater to avoid a stale closure
        return prev.filter((t) => t.id !== task.id);
      });
      try {
        await data.remove(task.id);
      } catch (error) {
        setTasks(snapshot);
        handledExpiry(error);
      }
    },
    [data, handledExpiry],
  );

  return { tasks, status, reload, create, changeStatus, rename, remove };
}
