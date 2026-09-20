import { LogoutIcon } from './icons';

interface AppHeaderProps {
  email: string;
  onSignOut: () => void;
}

export function AppHeader({ email, onSignOut }: AppHeaderProps) {
  const initial = email.charAt(0).toUpperCase();
  return (
    <header className="appbar">
      <div className="brand">
        <span className="logo">
          Task<b>ly</b>
        </span>
        <span className="tag">tasks</span>
      </div>
      <div className="userchip">
        <span className="avatar" aria-hidden="true">
          {initial}
        </span>
        <span>{email}</span>
        <button type="button" title="Sign out" aria-label="Sign out" onClick={onSignOut}>
          <LogoutIcon />
        </button>
      </div>
    </header>
  );
}
