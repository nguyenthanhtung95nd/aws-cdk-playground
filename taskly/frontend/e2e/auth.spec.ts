import { test, expect } from './fixtures';

test.describe('auth', () => {
  test('sign in reveals the seeded task list', async ({ page }) => {
    await page.goto('/');

    await page.getByLabel('Email').fill('demo@taskly.local');
    await page.getByLabel('Password').fill('any-password');
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible();
    await expect(page.getByLabel('New task title')).toBeVisible();
    await expect(page.getByText('Style the task list')).toBeVisible();
    await expect(page.getByRole('listitem')).toHaveCount(3);
  });

  test('signedIn fixture starts inside the workspace', async ({ signedIn }) => {
    await expect(signedIn.getByRole('button', { name: 'Sign out' })).toBeVisible();
    await expect(signedIn.getByRole('listitem')).toHaveCount(3);
  });
});
