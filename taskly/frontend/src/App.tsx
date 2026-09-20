import { useMemo } from 'react';
import { createServices } from './services/createServices';
import { useAuth } from './hooks/useAuth';
import { SignIn } from './components/SignIn';
import { Workspace } from './components/Workspace';

export default function App() {
  // Built once: mock services locally (VITE_USE_MOCKS=true) or real AWS services otherwise.
  const services = useMemo(() => createServices(), []);
  const { email, ready, signedIn, signIn, signOut } = useAuth(services.auth);

  if (!ready) {
    return (
      <div className="shell">
        <p className="footer app-splash">Loading…</p>
      </div>
    );
  }

  if (!signedIn || email === null) {
    return <SignIn onSignIn={signIn} />;
  }

  return <Workspace taskService={services.tasks} email={email} onSignOut={signOut} />;
}
