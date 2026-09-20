import { useCallback, useEffect, useMemo, useState } from 'react';
import type { NewProduct, Product } from '../types/product';
import { createProductGateway } from '../services/createServices';

type Status = 'loading' | 'ready' | 'error';

export function useProducts() {
  const gateway = useMemo(() => createProductGateway(), []);
  const [products, setProducts] = useState<Product[]>([]);
  const [status, setStatus] = useState<Status>('loading');
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setStatus('loading');
    setError(null);
    try {
      setProducts(await gateway.list());
      setStatus('ready');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load products');
      setStatus('error');
    }
  }, [gateway]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const create = useCallback(
    async (input: NewProduct) => {
      const product = await gateway.create(input);
      setProducts((prev) => [product, ...prev]);
    },
    [gateway],
  );

  const remove = useCallback(
    async (id: string) => {
      await gateway.remove(id);
      setProducts((prev) => prev.filter((product) => product.id !== id));
    },
    [gateway],
  );

  return { products, status, error, reload, create, remove };
}
