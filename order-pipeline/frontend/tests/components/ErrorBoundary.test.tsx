import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ErrorBoundary } from '../../src/components/ErrorBoundary';

function Exploding(): never {
  throw new Error('amount is not a number');
}

describe('ErrorBoundary', () => {
  it('shows the fallback instead of letting the failure reach the page', () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);

    render(
      <div>
        <p>Form still here</p>
        <ErrorBoundary fallback={(failure) => <p>Could not show: {failure.message}</p>}>
          <Exploding />
        </ErrorBoundary>
      </div>,
    );

    expect(screen.getByText(/Could not show: amount is not a number/)).toBeInTheDocument();
    expect(screen.getByText('Form still here')).toBeInTheDocument();
  });

  it('renders its children when nothing fails', () => {
    render(
      <ErrorBoundary fallback={() => <p>fallback</p>}>
        <p>content</p>
      </ErrorBoundary>,
    );

    expect(screen.getByText('content')).toBeInTheDocument();
    expect(screen.queryByText('fallback')).not.toBeInTheDocument();
  });
});
