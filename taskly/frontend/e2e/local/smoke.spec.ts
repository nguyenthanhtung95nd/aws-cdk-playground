import { test, expect } from '../fixtures';

// The real local table starts empty, so this test creates, drives, and deletes
// its own task, scoping every action by a per-run unique title.
test.describe('local backend smoke', () => {
  test('full task lifecycle against the local API', async ({ signedIn }) => {
    const title = `e2e-smoke ${Date.now()}`;
    const renamed = `${title} (edited)`;

    await signedIn.getByLabel('New task title').fill(title);
    await signedIn.getByRole('button', { name: 'Add task' }).click();
    const row = signedIn.getByRole('listitem').filter({ hasText: title });
    await expect(row).toBeVisible();

    await row.getByRole('button', { name: 'Status: To do' }).click();
    await expect(row.getByRole('button', { name: 'Status: Doing' })).toBeVisible();

    await row.getByRole('button', { name: 'Edit task' }).click();
    await signedIn.getByLabel('Edit task title').fill(renamed);
    await signedIn.getByLabel('Edit task title').press('Enter');
    const editedRow = signedIn.getByRole('listitem').filter({ hasText: renamed });
    await expect(editedRow).toBeVisible();

    await editedRow.getByRole('button', { name: 'Delete task' }).click();
    await expect(signedIn.getByText(renamed, { exact: true })).toHaveCount(0);
  });
});
