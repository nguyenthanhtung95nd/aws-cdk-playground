import type { Product } from '../types/product';

export function ProductCard({
  product,
  onDelete,
}: {
  product: Product;
  onDelete: (id: string) => void;
}) {
  return (
    <article className="card">
      {product.imageUrl ? (
        <img className="card__image" src={product.imageUrl} alt={product.name} />
      ) : (
        <div className="card__image card__image--empty" aria-hidden="true" />
      )}
      <h3 className="card__title">{product.name}</h3>
      <p className="card__desc">{product.description}</p>
      <p className="card__price">${product.price.toFixed(2)}</p>
      <button type="button" onClick={() => onDelete(product.id)} aria-label={`Delete ${product.name}`}>
        Delete
      </button>
    </article>
  );
}
