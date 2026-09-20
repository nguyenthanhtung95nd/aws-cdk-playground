import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CreateProductForm } from '../../src/components/CreateProductForm';

describe('CreateProductForm', () => {
  it('shows an error and does not submit when required fields are missing', async () => {
    const onCreate = vi.fn().mockResolvedValue(undefined);
    render(<CreateProductForm onCreate={onCreate} />);

    await userEvent.type(screen.getByLabelText('Name'), 'Hat');
    await userEvent.click(screen.getByRole('button', { name: 'Add product' }));

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(onCreate).not.toHaveBeenCalled();
  });

  it('exposes accessible labels for every field', () => {
    render(<CreateProductForm onCreate={vi.fn()} />);
    expect(screen.getByLabelText('Name')).toBeInTheDocument();
    expect(screen.getByLabelText('Description')).toBeInTheDocument();
    expect(screen.getByLabelText('Price')).toBeInTheDocument();
    expect(screen.getByLabelText('Image')).toBeInTheDocument();
  });
});
