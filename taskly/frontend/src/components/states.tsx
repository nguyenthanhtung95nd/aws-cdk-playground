export function LoadingState() {
  return (
    <>
      <ul className="tasks" aria-hidden="true">
        {[0, 1, 2].map((i) => (
          <li key={i} className="skeleton">
            <span className="sk chip" />
            <span className="sk line" />
          </li>
        ))}
      </ul>
      <p className="footer">Loading your tasks…</p>
    </>
  );
}

export function EmptyState() {
  return (
    <div className="panel">
      <div className="emoji" aria-hidden="true">
        🗒️
      </div>
      <h2>No tasks yet</h2>
      <p>Add your first task above to get started.</p>
    </div>
  );
}

export function ErrorState({ onRetry }: { onRetry: () => void }) {
  return (
    <div className="panel error" role="alert">
      <div className="emoji" aria-hidden="true">
        ⚠️
      </div>
      <h2>Couldn't load your tasks</h2>
      <p>Something went wrong reaching the server. Check your connection and try again.</p>
      <button type="button" className="btn-primary" onClick={onRetry}>
        Try again
      </button>
    </div>
  );
}

export function ExpiredBanner({ onReauth }: { onReauth: () => void }) {
  return (
    <div className="banner" role="alert">
      <span>
        <strong>Your session expired.</strong> Please sign in again to continue.
      </span>
      <button type="button" onClick={onReauth}>
        Sign in
      </button>
    </div>
  );
}
