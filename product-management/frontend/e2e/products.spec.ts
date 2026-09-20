import { test, expect } from '@playwright/test';

// Full lifecycle against mock mode. Locators use roles/labels only; assertions are web-first
// (auto-waiting), so there are no fixed sleeps.
test('create, list, and delete a product', async ({ page }) => {
  await page.goto('/');

  // The mock catalog is seeded with one product.
  await expect(page.getByRole('heading', { name: 'Sample Tee' })).toBeVisible();

  // Create a product.
  await page.getByLabel('Name').fill('E2E Hat');
  await page.getByLabel('Description').fill('A test hat');
  await page.getByLabel('Price').fill('12');
  await page.getByLabel('Image').setInputFiles({
    name: 'hat.png',
    mimeType: 'image/png',
    buffer: Buffer.from('fake-png-bytes'),
  });
  await page.getByRole('button', { name: 'Add product' }).click();

  // It appears in the catalog.
  await expect(page.getByRole('heading', { name: 'E2E Hat' })).toBeVisible();

  // Delete it.
  await page.getByRole('button', { name: 'Delete E2E Hat' }).click();
  await expect(page.getByRole('heading', { name: 'E2E Hat' })).toHaveCount(0);
});
