import type { NewProduct, Product } from '../../types/product';
import type { ProductGateway } from '../gateways';

/** In-memory product gateway for mock mode: deterministic, zero backend. */
export class MockProductDataService implements ProductGateway {
  private products: Product[] = [
    {
      id: 'seed-1',
      name: 'Sample Tee',
      description: 'A soft cotton tee',
      price: 25,
      imageUrl: '',
      createdAt: new Date(0).toISOString(),
      updatedAt: new Date(0).toISOString(),
    },
  ];
  private nextId = 1;

  async list(): Promise<Product[]> {
    return [...this.products].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  }

  async create(input: NewProduct): Promise<Product> {
    const now = new Date().toISOString();
    const product: Product = {
      id: `mock-${this.nextId++}`,
      name: input.name,
      description: input.description,
      price: input.price,
      imageUrl: input.imageData,
      createdAt: now,
      updatedAt: now,
    };
    this.products = [product, ...this.products];
    return product;
  }

  async remove(id: string): Promise<void> {
    this.products = this.products.filter((product) => product.id !== id);
  }
}
