import { useCallback, useMemo, useState } from 'react';
import type { TaskGateway } from '../services/gateways';
import { useTasks } from '../hooks/useTasks';
import { TASK_STATUSES } from '../types/task';
import { AppHeader } from './AppHeader';
import { CreateTaskForm } from './tasks/CreateTaskForm';
import { Filters } from './tasks/Filters';
import type { Filter } from './tasks/Filters';
import { STATUS_LABEL } from '../types/task';
import { TaskList } from './tasks/TaskList';
import { EmptyState, ErrorState, ExpiredBanner, LoadingState } from './states';

interface WorkspaceProps {
  taskService: TaskGateway;
  email: string;
  onSignOut: () => void;
}

export function Workspace({ taskService, email, onSignOut }: WorkspaceProps) {
  const [expired, setExpired] = useState(false);
  // Stable callback: keeps useTasks' effect from re-running on every render.
  const onExpired = useCallback(() => setExpired(true), []);
  const { tasks, status, reload, create, changeStatus, rename, remove } = useTasks(taskService, onExpired);
  const [filter, setFilter] = useState<Filter>('all');

  const counts = useMemo(() => {
    const c: Record<Filter, number> = { all: tasks.length, todo: 0, doing: 0, done: 0 };
    for (const t of tasks) c[t.status] += 1;
    return c;
  }, [tasks]);

  const visible = filter === 'all' ? tasks : tasks.filter((t) => t.status === filter);
  const openCount = counts.todo + counts.doing;

  return (
    <div className="shell">
      <AppHeader email={email} onSignOut={onSignOut} />
      <main>
        {expired && <ExpiredBanner onReauth={onSignOut} />}

        <CreateTaskForm disabled={expired} onCreate={create} />

        {status === 'loading' && <LoadingState />}
        {status === 'error' && <ErrorState onRetry={reload} />}

        {status === 'ready' && (
          <>
            <Filters current={filter} counts={counts} onChange={setFilter} />
            {tasks.length === 0 ? (
              <EmptyState />
            ) : visible.length === 0 ? (
              <div className="panel">
                <div className="emoji" aria-hidden="true">
                  🔍
                </div>
                <h2>Nothing in “{STATUS_LABEL[filter as (typeof TASK_STATUSES)[number]]}”</h2>
                <p>Switch filters or add a new task.</p>
              </div>
            ) : (
              <TaskList
                tasks={visible}
                onCycle={changeStatus}
                onRename={rename}
                onDelete={remove}
              />
            )}
            <p className="footer">
              {openCount} open · {counts.done} done · {counts.all} total
            </p>
          </>
        )}
      </main>
    </div>
  );
}
