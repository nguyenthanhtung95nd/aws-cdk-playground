import type { NewProduct, Product } from '../types/product';

/**
 * The single boundary the UI talks to. A mock and an HTTP implementation both satisfy it, so
 * components never change between mock / local / live modes.
 */
export interface ProductGateway {
  list(): Promise<Product[]>;
  create(input: NewProduct): Promise<Product>;
  remove(id: string): Promise<void>;
}
