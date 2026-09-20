import type { TaskStatus } from '../../types/task';
import { STATUS_LABEL } from '../../types/task';

interface StatusControlProps {
  status: TaskStatus;
  onClick: () => void;
}

/** A chip whose colour encodes the status; clicking advances to the next status. */
export function StatusControl({ status, onClick }: StatusControlProps) {
  return (
    <button
      type="button"
      className={`status ${status}`}
      onClick={onClick}
      aria-label={`Status: ${STATUS_LABEL[status]}. Click to advance.`}
    >
      <span className="dot" aria-hidden="true" />
      {STATUS_LABEL[status]}
    </button>
  );
}
