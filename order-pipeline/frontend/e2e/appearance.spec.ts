import { expect, placeOrder, shot, test, uniqueCustomer } from './fixtures';

test('every control can be reached by keyboard and shows where focus is', async ({ page }) => {
  await page.goto('/');

  for (const label of ['Customer', 'Product', 'Amount', 'Notes']) {
    await page.keyboard.press('Tab');
    await expect(page.getByLabel(label)).toBeFocused();
  }

  await page.keyboard.press('Tab');
  await expect(page.getByRole('button', { name: 'Place order' })).toBeFocused();

  const outline = await page
    .getByRole('button', { name: 'Place order' })
    .evaluate((element) => getComputedStyle(element).outlineWidth);
  expect(outline).not.toBe('0px');

  await shot(page, 'focus');
});

test('fits a narrow screen without sideways scrolling', async ({ page }) => {
  await page.setViewportSize({ width: 380, height: 780 });
  await page.goto('/');
  await placeOrder(page, { customer: uniqueCustomer('Narrow'), product: 'Headphones', amount: '1499' });

  await expect(page.getByRole('table')).toBeVisible();

  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);

  await shot(page, 'narrow');
});

test('fits a wide screen without sideways scrolling', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  await placeOrder(page, { customer: uniqueCustomer('Wide'), product: 'Headphones', amount: '1499' });

  await expect(page.getByRole('table')).toBeVisible();

  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);

  await shot(page, 'wide');
});
