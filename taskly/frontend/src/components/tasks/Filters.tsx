import type { TaskStatus } from '../../types/task';
import { STATUS_LABEL, TASK_STATUSES } from '../../types/task';

export type Filter = 'all' | TaskStatus;

interface FiltersProps {
  current: Filter;
  counts: Record<Filter, number>;
  onChange: (filter: Filter) => void;
}

export function Filters({ current, counts, onChange }: FiltersProps) {
  const options: Filter[] = ['all', ...TASK_STATUSES];
  return (
    <div className="filters" role="group" aria-label="Filter tasks">
      {options.map((f) => (
        <button
          key={f}
          type="button"
          aria-pressed={current === f}
          onClick={() => onChange(f)}
        >
          {f === 'all' ? 'All' : STATUS_LABEL[f]}
          <span className="count">{counts[f]}</span>
        </button>
      ))}
    </div>
  );
}
