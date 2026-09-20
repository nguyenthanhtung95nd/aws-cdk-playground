import { useState, type FormEvent } from 'react';

interface SignInProps {
  onSignIn: (email: string, password: string) => Promise<void>;
}

export function SignIn({ onSignIn }: SignInProps) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await onSignIn(email, password);
    } catch {
      setError('Sign-in failed. Check your email and password.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="shell">
      <div className="signin">
        <div className="logo">
          Task<b>ly</b>
        </div>
        <p>Sign in to manage your tasks.</p>
        <form className="card" onSubmit={submit}>
          <div className="field">
            <label htmlFor="email">Email</label>
            <input
              id="email"
              type="email"
              value={email}
              autoComplete="username"
              onChange={(e) => setEmail(e.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="password">Password</label>
            <input
              id="password"
              type="password"
              value={password}
              autoComplete="current-password"
              onChange={(e) => setPassword(e.target.value)}
            />
          </div>
          {error && (
            <p className="form-error" role="alert">
              {error}
            </p>
          )}
          <button type="submit" className="btn-primary" disabled={busy}>
            {busy ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
      </div>
    </div>
  );
}
