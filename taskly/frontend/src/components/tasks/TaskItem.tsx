import { memo, useState } from 'react';
import type { Task } from '../../types/task';
import { StatusControl } from './StatusControl';
import { EditIcon, TrashIcon } from '../icons';

interface TaskItemProps {
  task: Task;
  onCycle: (task: Task) => void;
  onRename: (task: Task, title: string) => void;
  onDelete: (task: Task) => void;
}

function TaskItemComponent({ task, onCycle, onRename, onDelete }: TaskItemProps) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(task.title);

  const commit = () => {
    const value = draft.trim();
    if (value && value !== task.title) onRename(task, value);
    setEditing(false);
  };

  const startEdit = () => {
    setDraft(task.title);
    setEditing(true);
  };

  return (
    <li className={`task ${task.status}`}>
      <StatusControl status={task.status} onClick={() => onCycle(task)} />

      {editing ? (
        <input
          className="title-edit"
          value={draft}
          autoFocus
          aria-label="Edit task title"
          onChange={(e) => setDraft(e.target.value)}
          onBlur={commit}
          onKeyDown={(e) => {
            if (e.key === 'Enter') commit();
            if (e.key === 'Escape') setEditing(false);
          }}
        />
      ) : (
        <span className="title" title="Click to edit" onClick={startEdit}>
          {task.title}
        </span>
      )}

      <div className="rowactions">
        <button type="button" className="iconbtn" aria-label="Edit task" onClick={startEdit}>
          <EditIcon />
        </button>
        <button
          type="button"
          className="iconbtn danger"
          aria-label="Delete task"
          onClick={() => onDelete(task)}
        >
          <TrashIcon />
        </button>
      </div>
    </li>
  );
}

// A task row only needs to re-render when its own task or handlers change.
export const TaskItem = memo(TaskItemComponent);
