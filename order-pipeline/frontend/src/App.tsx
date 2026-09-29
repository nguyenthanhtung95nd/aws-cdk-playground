import { ErrorBoundary } from './components/ErrorBoundary';
import { OrderList } from './components/OrderList';
import { PlaceOrderForm } from './components/PlaceOrderForm';
import { EmptyState, ErrorState, LoadingState, RenderFailureState, StaleBanner } from './components/states';
import { useOrders } from './hooks/useOrders';

// The list holds one page, so saying "3 orders" would be a claim about the whole system that the
// page cannot make.
function summarise(shown: number, hasMore: boolean): string {
  return hasMore ? `newest ${String(shown)} orders` : `${String(shown)} orders`;
}

export function App() {
  const { state, place, reload } = useOrders();

  return (
    <main>
      <h1>Order Pipeline</h1>
      <PlaceOrderForm onPlace={place} />

      <section aria-labelledby="orders-heading">
        <h2 id="orders-heading">Orders</h2>
        <p className="summary" role="status" aria-live="polite">
          {state.status === 'ready' ? summarise(state.orders.length, state.hasMore) : ''}
        </p>
        {state.status === 'loading' ? <LoadingState /> : null}
        {state.status === 'failed' ? (
          <ErrorState message={state.error} onRetry={() => void reload()} />
        ) : null}
        {state.status === 'ready' ? (
          <>
            {state.staleError ? <StaleBanner message={state.staleError} /> : null}
            {state.orders.length === 0 ? (
              <EmptyState />
            ) : (
              <ErrorBoundary fallback={(failure) => <RenderFailureState message={failure.message} />}>
                <OrderList orders={state.orders} />
              </ErrorBoundary>
            )}
          </>
        ) : null}
      </section>
    </main>
  );
}
