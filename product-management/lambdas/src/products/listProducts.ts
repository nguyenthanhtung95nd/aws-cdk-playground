import { Product } from './model/product';
import { scanProducts } from './productRepository';

export async function listProducts(): Promise<Product[]> {
  return scanProducts();
}
