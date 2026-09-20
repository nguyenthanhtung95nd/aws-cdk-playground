export interface AppConfig {
  /** When true, the app uses in-memory mock services and never touches a backend. */
  useMocks: boolean;
  /** When true, use a fake token instead of Cognito (for the local backend, which has no Cognito). */
  devAuth: boolean;
  apiUrl: string;
  userPoolId: string;
  userPoolClientId: string;
  region: string;
}

const useMocks = import.meta.env.VITE_USE_MOCKS === 'true';
const devAuth = import.meta.env.VITE_DEV_AUTH === 'true';

function required(name: string, value: string | undefined): string {
  if (!value) {
    throw new Error(`Missing ${name}. Set it in .env, or run "npm run dev:mock" / "npm run dev:local".`);
  }
  return value;
}

// Three shapes:
//  - mock:  fully in-memory, no backend, no env needed        (npm run dev:mock)
//  - local: real API client -> localhost, fake auth token     (npm run dev:local + local backend)
//  - live:  real API client -> deployed API, Cognito auth      (npm run dev + .env)
export const config: AppConfig = useMocks
  ? { useMocks: true, devAuth: false, apiUrl: '', userPoolId: '', userPoolClientId: '', region: '' }
  : devAuth
    ? {
        useMocks: false,
        devAuth: true,
        apiUrl: required('VITE_API_URL', import.meta.env.VITE_API_URL),
        userPoolId: '',
        userPoolClientId: '',
        region: import.meta.env.VITE_AWS_REGION ?? 'us-west-1',
      }
    : {
        useMocks: false,
        devAuth: false,
        apiUrl: required('VITE_API_URL', import.meta.env.VITE_API_URL),
        userPoolId: required('VITE_USER_POOL_ID', import.meta.env.VITE_USER_POOL_ID),
        userPoolClientId: required('VITE_USER_POOL_CLIENT_ID', import.meta.env.VITE_USER_POOL_CLIENT_ID),
        region: required('VITE_AWS_REGION', import.meta.env.VITE_AWS_REGION),
      };
