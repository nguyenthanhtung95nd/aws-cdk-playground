import { Amplify } from 'aws-amplify';
import { fetchAuthSession, getCurrentUser, signIn, signOut } from 'aws-amplify/auth';
import { config } from '../config';
import type { AuthGateway } from './gateways';
import { SessionExpiredError } from './errors';

export class AuthService implements AuthGateway {
  constructor() {
    Amplify.configure({
      Auth: {
        Cognito: {
          userPoolId: config.userPoolId,
          userPoolClientId: config.userPoolClientId,
        },
      },
    });
  }

  /** Returns the signed-in user's login id, or null if there is no active session. */
  async currentEmail(): Promise<string | null> {
    try {
      const user = await getCurrentUser();
      return user.signInDetails?.loginId ?? user.username;
    } catch {
      return null;
    }
  }

  async signIn(email: string, password: string): Promise<void> {
    await signIn({ username: email, password, options: { authFlowType: 'USER_PASSWORD_AUTH' } });
  }

  async signOut(): Promise<void> {
    await signOut();
  }

  /** The id token sent as the Authorization header. A missing token means the session is gone. */
  async getIdToken(): Promise<string> {
    const session = await fetchAuthSession();
    const token = session.tokens?.idToken?.toString();
    if (!token) {
      throw new SessionExpiredError('No active session');
    }
    return token;
  }
}
