import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { PlaceOrderForm } from '../../src/components/PlaceOrderForm';

describe('PlaceOrderForm', () => {
  it('marks every field the server requires', () => {
    render(<PlaceOrderForm onPlace={vi.fn()} />);

    expect(screen.getByLabelText('Customer')).toBeRequired();
    expect(screen.getByLabelText('Product')).toBeRequired();
    expect(screen.getByLabelText('Amount')).toBeRequired();
    expect(screen.getByLabelText('Notes')).not.toBeRequired();
  });

  it('refuses to send an order with an empty amount', async () => {
    const onPlace = vi.fn();
    const user = userEvent.setup();
    render(<PlaceOrderForm onPlace={onPlace} />);

    await user.type(screen.getByLabelText('Customer'), 'Alice');
    await user.type(screen.getByLabelText('Product'), 'Headphones');
    await user.click(screen.getByRole('button', { name: 'Place order' }));

    expect(onPlace).not.toHaveBeenCalled();
    expect(screen.getByText('Amount is required.')).toBeInTheDocument();
  });

  it('points the field at its own error message', async () => {
    const user = userEvent.setup();
    render(<PlaceOrderForm onPlace={vi.fn()} />);

    await user.click(screen.getByRole('button', { name: 'Place order' }));

    const amount = screen.getByLabelText('Amount');
    const describedBy = amount.getAttribute('aria-describedby');
    expect(describedBy).not.toBeNull();
    expect(document.getElementById(describedBy ?? '')).toHaveTextContent('Amount is required.');
    expect(amount).toHaveAttribute('aria-invalid', 'true');
  });

  it('clears a field error as soon as the field is corrected', async () => {
    const user = userEvent.setup();
    render(<PlaceOrderForm onPlace={vi.fn()} />);

    await user.click(screen.getByRole('button', { name: 'Place order' }));
    expect(screen.getByText('Customer is required.')).toBeInTheDocument();

    await user.type(screen.getByLabelText('Customer'), 'Alice');
    expect(screen.queryByText('Customer is required.')).not.toBeInTheDocument();
  });

  it('sends the order once every required field is filled', async () => {
    const onPlace = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(<PlaceOrderForm onPlace={onPlace} />);

    await user.type(screen.getByLabelText('Customer'), 'Alice');
    await user.type(screen.getByLabelText('Product'), 'Headphones');
    await user.type(screen.getByLabelText('Amount'), '1499');
    await user.click(screen.getByRole('button', { name: 'Place order' }));

    expect(onPlace).toHaveBeenCalledWith({
      customerName: 'Alice',
      product: 'Headphones',
      amount: 1499,
      notes: '',
    });
  });

  it('shows the message the api sends back when placing fails', async () => {
    const onPlace = vi.fn().mockRejectedValue(new Error('amount must be greater than zero.'));
    const user = userEvent.setup();
    render(<PlaceOrderForm onPlace={onPlace} />);

    await user.type(screen.getByLabelText('Customer'), 'Alice');
    await user.type(screen.getByLabelText('Product'), 'Headphones');
    await user.type(screen.getByLabelText('Amount'), '1');
    await user.click(screen.getByRole('button', { name: 'Place order' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('amount must be greater than zero.');
  });
});
