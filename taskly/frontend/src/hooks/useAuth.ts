import { useCallback, useEffect, useState } from 'react';
import type { AuthGateway } from '../services/gateways';

export function useAuth(auth: AuthGateway) {
  const [email, setEmail] = useState<string | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    auth
      .currentEmail()
      .then(setEmail)
      .finally(() => setReady(true));
  }, [auth]);

  const signIn = useCallback(
    async (emailInput: string, password: string) => {
      await auth.signIn(emailInput, password);
      setEmail(await auth.currentEmail());
    },
    [auth],
  );

  const signOut = useCallback(async () => {
    await auth.signOut();
    setEmail(null);
  }, [auth]);

  return { email, ready, signedIn: email !== null, signIn, signOut };
}
