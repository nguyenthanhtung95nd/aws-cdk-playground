import type { Page } from '@playwright/test';

export { expect, test } from '@playwright/test';

export interface OrderInput {
  customer: string;
  product: string;
  amount: string;
}

export function uniqueCustomer(prefix: string): string {
  return `${prefix} ${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 6)}`;
}

export async function placeOrder(page: Page, order: OrderInput): Promise<void> {
  await page.getByLabel('Customer').fill(order.customer);
  await page.getByLabel('Product').fill(order.product);
  await page.getByLabel('Amount').fill(order.amount);
  await page.getByRole('button', { name: 'Place order' }).click();
  await page.getByRole('cell', { name: order.customer }).waitFor();
}

export async function shot(page: Page, name: string): Promise<void> {
  await page.screenshot({ path: `e2e/screenshots/${name}.png`, fullPage: true });
}
