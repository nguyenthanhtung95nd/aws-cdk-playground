import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TaskItem } from '../../../src/components/tasks/TaskItem';
import type { Task } from '../../../src/types/task';

const task: Task = {
  userId: 'u1',
  id: 't1',
  title: 'Buy milk',
  status: 'todo',
  createdAt: '',
  updatedAt: '',
};

const noop = () => {};

describe('TaskItem', () => {
  it('shows the title and current status', () => {
    render(<TaskItem task={task} onCycle={noop} onRename={noop} onDelete={noop} />);
    expect(screen.getByText('Buy milk')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /status: to do/i })).toBeInTheDocument();
  });

  it('advances status on chip click and deletes on trash click', async () => {
    const onCycle = vi.fn();
    const onDelete = vi.fn();
    render(<TaskItem task={task} onCycle={onCycle} onRename={noop} onDelete={onDelete} />);

    await userEvent.click(screen.getByRole('button', { name: /status: to do/i }));
    expect(onCycle).toHaveBeenCalledWith(task);

    await userEvent.click(screen.getByRole('button', { name: /delete task/i }));
    expect(onDelete).toHaveBeenCalledWith(task);
  });

  it('renames on Enter after editing', async () => {
    const onRename = vi.fn();
    render(<TaskItem task={task} onCycle={noop} onRename={onRename} onDelete={noop} />);

    await userEvent.click(screen.getByRole('button', { name: /edit task/i }));
    const input = screen.getByRole('textbox', { name: /edit task title/i });
    await userEvent.clear(input);
    await userEvent.type(input, 'Buy oat milk{Enter}');
    expect(onRename).toHaveBeenCalledWith(task, 'Buy oat milk');
  });
});
