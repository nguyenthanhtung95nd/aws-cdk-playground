import { useState, type FormEvent } from 'react';

interface CreateTaskFormProps {
  disabled?: boolean;
  onCreate: (title: string) => Promise<void>;
}

export function CreateTaskForm({ disabled, onCreate }: CreateTaskFormProps) {
  const [title, setTitle] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const value = title.trim();
    if (!value || busy) return;
    setBusy(true);
    setError(null);
    try {
      await onCreate(value);
      setTitle('');
    } catch {
      // onCreate rethrows non-expiry failures; keep the text and tell the user.
      setError('Could not add the task. Please try again.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <form className="addform" onSubmit={submit}>
        <input
          type="text"
          value={title}
          placeholder="Add a task and press Enter…"
          aria-label="New task title"
          autoComplete="off"
          disabled={disabled}
          onChange={(e) => setTitle(e.target.value)}
        />
        <button type="submit" className="btn-primary" disabled={disabled || busy || !title.trim()}>
          Add task
        </button>
      </form>
      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
