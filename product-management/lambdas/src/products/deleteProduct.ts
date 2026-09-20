import { NotFoundError } from './shared/errors';
import { deleteProductImage } from './imageStore';
import { getProduct, deleteProduct as deleteProductRecord } from './productRepository';

/**
 * Deletes a product: verify it exists (404 otherwise) -> remove its S3 image -> remove the record.
 * Image cleanup is best-effort: a failed S3 delete is logged but must not block record removal.
 */
export async function deleteProduct(id: string): Promise<void> {
  const product = await getProduct(id);
  if (!product) {
    throw new NotFoundError(`Product ${id} not found`);
  }

  if (product.imageUrl) {
    try {
      await deleteProductImage(product.imageUrl);
    } catch (error) {
      console.error('Failed to delete product image', error);
    }
  }

  await deleteProductRecord(id);
}
