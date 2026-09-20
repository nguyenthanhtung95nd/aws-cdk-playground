import type { Product } from '../types/product';
import { ProductCard } from './ProductCard';
import { EmptyState } from './states';

export function ProductList({
  products,
  onDelete,
}: {
  products: Product[];
  onDelete: (id: string) => void;
}) {
  if (products.length === 0) {
    return <EmptyState />;
  }

  return (
    <ul className="grid" aria-label="Product catalog">
      {products.map((product) => (
        <li key={product.id}>
          <ProductCard product={product} onDelete={onDelete} />
        </li>
      ))}
    </ul>
  );
}
