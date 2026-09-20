import { config } from '../config';
import type { AuthGateway, TaskGateway } from './gateways';
import { AuthService } from './AuthService';
import { TaskDataService } from './TaskDataService';
import { MockAuthService } from './mocks/MockAuthService';
import { MockTaskDataService } from './mocks/MockTaskDataService';

export interface Services {
  auth: AuthGateway;
  tasks: TaskGateway;
}

/**
 * Builds the app's services once. In mock mode (`VITE_USE_MOCKS=true`) both are in-memory, so the
 * app runs fully local with no AWS. Otherwise they talk to the deployed Cognito + HTTP API.
 */
export function createServices(): Services {
  if (config.useMocks) {
    return { auth: new MockAuthService(), tasks: new MockTaskDataService() };
  }
  // devAuth = local backend (no Cognito): fake token, but a real API client hitting the API URL.
  const auth = config.devAuth ? new MockAuthService() : new AuthService();
  return { auth, tasks: new TaskDataService(config.apiUrl, auth) };
}
