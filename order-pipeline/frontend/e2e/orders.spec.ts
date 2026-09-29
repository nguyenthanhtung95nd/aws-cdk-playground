import { expect, placeOrder, shot, test, uniqueCustomer } from './fixtures';

test('places an order and shows it without a reload', async ({ page }) => {
  const customer = uniqueCustomer('Carol');
  await page.goto('/');

  await placeOrder(page, { customer, product: 'Keyboard', amount: '2500' });

  await expect(page.getByRole('cell', { name: customer })).toBeVisible();
  await expect(page.getByLabel('Customer')).toHaveValue('');

  await shot(page, 'placed');
});

test('keeps an order across a reload', async ({ page }) => {
  const customer = uniqueCustomer('Dana');
  await page.goto('/');
  await placeOrder(page, { customer, product: 'Monitor', amount: '399' });

  await page.reload();

  await expect(page.getByRole('cell', { name: customer })).toBeVisible();
});

test('flags an order above the high value threshold', async ({ page }) => {
  const customer = uniqueCustomer('Bob');
  await page.goto('/');

  await placeOrder(page, { customer, product: 'Standing desk', amount: '25000' });

  const row = page.getByRole('row').filter({ hasText: customer });
  await expect(row.getByText('high value')).toBeVisible();

  await shot(page, 'high-value');
});

test('refuses to send an order with an empty amount', async ({ page }) => {
  await page.goto('/');

  await page.getByLabel('Customer').fill('Dan');
  await page.getByLabel('Product').fill('Mouse');
  await page.getByRole('button', { name: 'Place order' }).click();

  await expect(page.getByText('Amount is required.')).toBeVisible();
  await expect(page.getByLabel('Amount')).toHaveAttribute('aria-invalid', 'true');

  await shot(page, 'validation');
});
