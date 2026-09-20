import { test, expect } from './fixtures';

// Rows are scoped by their title so an action (Delete/Edit/status) targets one
// task even when several rows expose the same button label.
test.describe('task CRUD', () => {
  test('create adds a task to the top of the list', async ({ signedIn }) => {
    const title = 'Write the e2e report';

    await signedIn.getByLabel('New task title').fill(title);
    await signedIn.getByRole('button', { name: 'Add task' }).click();

    await expect(signedIn.getByRole('listitem')).toHaveCount(4);
    await expect(signedIn.getByRole('listitem').first()).toContainText(title);
  });

  test('clicking the status chip advances todo to doing', async ({ signedIn }) => {
    const row = signedIn.getByRole('listitem').filter({ hasText: 'Style the task list' });

    await row.getByRole('button', { name: 'Status: To do' }).click();

    await expect(row.getByRole('button', { name: 'Status: Doing' })).toBeVisible();
  });

  test('editing a title replaces the old text', async ({ signedIn }) => {
    const row = signedIn.getByRole('listitem').filter({ hasText: 'Deploy the dev backend' });

    await row.getByRole('button', { name: 'Edit task' }).click();
    await signedIn.getByLabel('Edit task title').fill('Deploy the dev backend v2');
    await signedIn.getByLabel('Edit task title').press('Enter');

    await expect(signedIn.getByText('Deploy the dev backend v2')).toBeVisible();
    await expect(signedIn.getByText('Deploy the dev backend', { exact: true })).toHaveCount(0);
  });

  test('delete removes the task from the list', async ({ signedIn }) => {
    const row = signedIn.getByRole('listitem').filter({ hasText: 'Style the task list' });

    await row.getByRole('button', { name: 'Delete task' }).click();

    await expect(signedIn.getByText('Style the task list')).toHaveCount(0);
    await expect(signedIn.getByRole('listitem')).toHaveCount(2);
  });
});
