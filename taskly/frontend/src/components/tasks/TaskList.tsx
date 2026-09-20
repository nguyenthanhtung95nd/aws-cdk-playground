import type { Task } from '../../types/task';
import { TaskItem } from './TaskItem';

interface TaskListProps {
  tasks: Task[];
  onCycle: (task: Task) => void;
  onRename: (task: Task, title: string) => void;
  onDelete: (task: Task) => void;
}

export function TaskList({ tasks, onCycle, onRename, onDelete }: TaskListProps) {
  return (
    <ul className="tasks">
      {tasks.map((task) => (
        <TaskItem
          key={task.id}
          task={task}
          onCycle={onCycle}
          onRename={onRename}
          onDelete={onDelete}
        />
      ))}
    </ul>
  );
}
