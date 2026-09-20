import { test, expect } from './fixtures';

// Filter buttons are scoped inside the "Filter tasks" group so their names
// ("Done"/"Doing") do not collide with the status chips.
test.describe('filters and counts', () => {
  test('footer shows whole-list counts (2 open, 1 done, 3 total)', async ({ signedIn }) => {
    await expect(signedIn.getByText(/2 open.*1 done.*3 total/)).toBeVisible();
  });

  test('filtering by Done shows only the done task', async ({ signedIn }) => {
    const filters = signedIn.getByRole('group', { name: 'Filter tasks' });

    await filters.getByRole('button', { name: 'Done' }).click();

    await expect(signedIn.getByRole('listitem')).toHaveCount(1);
    await expect(signedIn.getByRole('listitem')).toContainText('Deploy the dev backend');
    // Counts stay whole-list, not the filtered view.
    await expect(signedIn.getByText(/2 open.*1 done.*3 total/)).toBeVisible();

    await filters.getByRole('button', { name: 'All' }).click();
    await expect(signedIn.getByRole('listitem')).toHaveCount(3);
  });
});
