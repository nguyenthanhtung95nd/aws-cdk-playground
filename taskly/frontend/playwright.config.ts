import { defineConfig, devices } from '@playwright/test';

// Runs against the deterministic mock build (npm run dev:mock): in-memory data,
// zero AWS, zero real network. Playwright owns the dev-server lifecycle below.
const PORT = 5173;
const baseURL = `http://localhost:${PORT}`;

export default defineConfig({
  testDir: './e2e',
  // e2e/local/** targets the real backend (playwright.local.config.ts), not mock.
  testIgnore: '**/local/**',
  forbidOnly: !!process.env.CI,
  // Mock mode is deterministic, so retries are a CI-only safety net.
  retries: process.env.CI ? 2 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npm run dev:mock',
    url: baseURL,
    // Reuse a server already running during local dev; always fresh in CI.
    reuseExistingServer: !process.env.CI,
    timeout: 60_000,
  },
});
