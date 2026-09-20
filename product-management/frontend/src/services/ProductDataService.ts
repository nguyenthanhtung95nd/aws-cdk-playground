import { config } from '../config';
import type { NewProduct, Product } from '../types/product';
import type { ProductGateway } from './gateways';

/** HTTP implementation of the product gateway; talks to the products API. */
export class ProductDataService implements ProductGateway {
  private readonly baseUrl = config.apiBaseUrl;

  async list(): Promise<Product[]> {
    const response = await fetch(`${this.baseUrl}/products`);
    if (!response.ok) throw new Error(`Failed to load products (${response.status})`);
    return (await response.json()) as Product[];
  }

  async create(input: NewProduct): Promise<Product> {
    const response = await fetch(`${this.baseUrl}/products`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(input),
    });
    if (!response.ok) throw new Error(`Failed to create product (${response.status})`);
    const data = (await response.json()) as { product: Product };
    return data.product;
  }

  async remove(id: string): Promise<void> {
    const response = await fetch(`${this.baseUrl}/products/${id}`, { method: 'DELETE' });
    if (!response.ok) throw new Error(`Failed to delete product (${response.status})`);
  }
}
