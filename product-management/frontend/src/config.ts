export interface AppConfig {
  /** When true, the app uses in-memory mock services and never calls a backend. */
  useMocks: boolean;
  /** Base URL of the products API (empty in mock mode). */
  apiBaseUrl: string;
}

export const config: AppConfig = {
  useMocks: import.meta.env.VITE_USE_MOCKS === 'true',
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? '',
};
