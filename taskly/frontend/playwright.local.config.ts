import { defineConfig, devices } from '@playwright/test';

// E2E against the REAL local backend (full-stack local): frontend dev:local ->
// handler on :4000 -> DynamoDB Local (Docker). Opt-in; requires Docker running.
// The local DB starts empty, so specs here are seed-independent and self-cleaning
// (unlike the deterministic mock suite in playwright.config.ts).
const WEB_PORT = 5173;
const API_URL = 'http://localhost:4000';
const baseURL = `http://localhost:${WEB_PORT}`;

export default defineConfig({
  testDir: './e2e/local',
  // One shared backend/table -> keep tests serial so they do not race on data.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL,
    // Headed locally so you can watch the run; headless in CI.
    headless: !!process.env.CI,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      // Backend reuses the real handler; auto-creates the table on boot.
      command: 'npm run dev',
      cwd: '../lambdas',
      url: `${API_URL}/tasks`,
      reuseExistingServer: true,
      timeout: 60_000,
    },
    {
      command: 'npm run dev:local',
      url: baseURL,
      reuseExistingServer: !process.env.CI,
      timeout: 60_000,
    },
  ],
});
