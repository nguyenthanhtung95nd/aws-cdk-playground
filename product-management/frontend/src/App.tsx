import { AppHeader } from './components/AppHeader';
import { CreateProductForm } from './components/CreateProductForm';
import { ProductList } from './components/ProductList';
import { LoadingState, ErrorState } from './components/states';
import { useProducts } from './hooks/useProducts';

export function App() {
  const { products, status, error, reload, create, remove } = useProducts();

  return (
    <>
      <AppHeader />
      <main className="container">
        <section aria-labelledby="add-heading">
          <h2 id="add-heading">Add a product</h2>
          <CreateProductForm onCreate={create} />
        </section>

        <section aria-labelledby="catalog-heading">
          <h2 id="catalog-heading">Catalog</h2>
          {status === 'loading' && <LoadingState />}
          {status === 'error' && error && <ErrorState message={error} onRetry={reload} />}
          {status === 'ready' && <ProductList products={products} onDelete={remove} />}
        </section>
      </main>
    </>
  );
}
