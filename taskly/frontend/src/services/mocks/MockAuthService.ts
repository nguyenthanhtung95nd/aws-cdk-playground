import type { AuthGateway } from '../gateways';

/** In-memory auth for local mock mode: any credentials sign in; no AWS involved. */
export class MockAuthService implements AuthGateway {
  private email: string | null = null;

  async currentEmail(): Promise<string | null> {
    return this.email;
  }

  async signIn(email: string): Promise<void> {
    this.email = email.trim() || 'demo@taskly.local';
  }

  async signOut(): Promise<void> {
    this.email = null;
  }

  async getIdToken(): Promise<string> {
    return 'mock-id-token';
  }
}
