export interface MessageProps {
  message: string;
}

export interface ErrorStateProps extends MessageProps {
  onRetry: () => void;
}

export function LoadingState() {
  return <p className="state">Loading orders...</p>;
}

export function EmptyState() {
  return (
    <div className="state">
      <p className="state-title">No orders yet</p>
      <p>Place one using the form above and watch it move through the pipeline.</p>
    </div>
  );
}

export function ErrorState({ message, onRetry }: ErrorStateProps) {
  return (
    <div className="state state-error">
      <p className="state-title">Could not reach the API</p>
      <p>{message}</p>
      <button type="button" onClick={onRetry}>
        Try again
      </button>
    </div>
  );
}

export function StaleBanner({ message }: MessageProps) {
  return (
    <p className="banner" role="status">
      Showing the last orders we could load. {message}
    </p>
  );
}

export function RenderFailureState({ message }: MessageProps) {
  return (
    <div className="state state-error" role="alert">
      <p className="state-title">This list could not be shown</p>
      <p>{message}</p>
      <p>Placing an order still works.</p>
    </div>
  );
}
