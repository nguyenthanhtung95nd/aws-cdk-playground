import { render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { Order } from '../../src/types/order';

const gateway = { list: vi.fn(), place: vi.fn() };

vi.mock('../../src/services/createServices', () => ({
  createOrderGateway: () => gateway,
}));

const { App } = await import('../../src/App');

const anOrder: Order = {
  orderId: 'a',
  customerName: 'Alice',
  product: 'Headphones',
  amount: 1499,
  notes: '',
  status: 'PENDING',
  isHighValue: false,
  placedAt: '2026-09-24T08:00:00Z',
};

beforeEach(() => {
  gateway.list.mockReset();
  gateway.place.mockReset();
});

describe('App', () => {
  it('announces how many orders there are, not what is in them', async () => {
    gateway.list.mockResolvedValue({
      orders: [anOrder, { ...anOrder, orderId: 'b', customerName: 'Bob' }],
      nextCursor: null,
    });

    render(<App />);

    const announcement = await screen.findByRole('status');
    expect(announcement).toHaveTextContent('2 orders');
    expect(announcement).not.toHaveTextContent('Alice');
    expect(announcement).not.toHaveTextContent('Headphones');
  });

  // The list holds one page, so a plain count would claim something about the whole system that
  // the page has no way of knowing.
  it('does not claim a total when there are more orders than it is showing', async () => {
    gateway.list.mockResolvedValue({ orders: [anOrder], nextCursor: 'more-please' });

    render(<App />);

    const announcement = await screen.findByRole('status');
    expect(announcement).toHaveTextContent('newest 1 orders');
  });

  it('shows the empty state rather than a bare table', async () => {
    gateway.list.mockResolvedValue({ orders: [], nextCursor: null });

    render(<App />);

    expect(await screen.findByText('No orders yet')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('keeps the form usable when the list cannot be loaded', async () => {
    gateway.list.mockRejectedValue(new Error('api is down'));

    render(<App />);

    await waitFor(() => expect(screen.getByText('Could not reach the API')).toBeInTheDocument());
    expect(screen.getByLabelText('Customer')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Place order' })).toBeEnabled();
  });
});
