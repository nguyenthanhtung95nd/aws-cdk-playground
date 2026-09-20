export function LoadingState() {
  return <p role="status">Loading products...</p>;
}

export function ErrorState({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div role="alert">
      <p>{message}</p>
      <button type="button" onClick={onRetry}>
        Retry
      </button>
    </div>
  );
}

export function EmptyState() {
  return <p>No products yet. Add your first one above.</p>;
}
