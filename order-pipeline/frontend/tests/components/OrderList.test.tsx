import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { OrderList } from '../../src/components/OrderList';
import type { Order } from '../../src/types/order';

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

describe('OrderList', () => {
  it('names the table for anyone who cannot see it', () => {
    render(<OrderList orders={[anOrder]} />);

    expect(screen.getByRole('table')).toHaveAccessibleName(/where each one has reached/i);
  });

  it('marks every header as a column header', () => {
    render(<OrderList orders={[anOrder]} />);

    const headers = screen.getAllByRole('columnheader');
    expect(headers).toHaveLength(4);
    for (const header of headers) {
      expect(header).toHaveAttribute('scope', 'col');
    }
  });

  it('flags a high value order in its own row', () => {
    render(<OrderList orders={[anOrder, { ...anOrder, orderId: 'b', isHighValue: true }]} />);

    const rows = screen.getAllByRole('row').slice(1);
    expect(within(rows[0]).queryByText('high value')).not.toBeInTheDocument();
    expect(within(rows[1]).getByText('high value')).toBeInTheDocument();
  });
});
