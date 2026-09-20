import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ProductCard } from '../../src/components/ProductCard';
import type { Product } from '../../src/types/product';

const product: Product = {
  id: 'p1',
  name: 'Sample Tee',
  description: 'A soft cotton tee',
  price: 25,
  imageUrl: '',
  createdAt: '',
  updatedAt: '',
};

describe('ProductCard', () => {
  it('renders the product name and formatted price', () => {
    render(<ProductCard product={product} onDelete={() => {}} />);
    expect(screen.getByRole('heading', { name: 'Sample Tee' })).toBeInTheDocument();
    expect(screen.getByText('$25.00')).toBeInTheDocument();
  });

  it('calls onDelete with the product id when the delete button is clicked', async () => {
    const onDelete = vi.fn();
    render(<ProductCard product={product} onDelete={onDelete} />);
    await userEvent.click(screen.getByRole('button', { name: 'Delete Sample Tee' }));
    expect(onDelete).toHaveBeenCalledWith('p1');
  });
});
