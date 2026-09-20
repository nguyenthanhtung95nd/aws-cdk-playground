import { test as base, expect, type Page } from '@playwright/test';

// Mock mode accepts any credentials; fixed values keep runs reproducible.
const EMAIL = 'demo@taskly.local';
const PASSWORD = 'any-password';

// `signedIn` yields a Page already inside the workspace so CRUD/filter specs
// skip the login steps. Anchored to accessibility roles/labels only.
export const test = base.extend<{ signedIn: Page }>({
  signedIn: async ({ page }, use) => {
    await page.goto('/');
    await page.getByLabel('Email').fill(EMAIL);
    await page.getByLabel('Password').fill(PASSWORD);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page.getByRole('button', { name: 'Add task' })).toBeVisible();
    await use(page);
  },
});

export { expect };
