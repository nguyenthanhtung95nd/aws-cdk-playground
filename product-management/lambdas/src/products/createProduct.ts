import { randomUUID } from 'node:crypto';
import { Product } from './model/product';
import { parseProductInput } from './shared/validator';
import { putProductImage } from './imageStore';
import { putProduct } from './productRepository';
import { enqueueProductCreated } from './queueClient';

/**
 * Creates a product: validate -> upload image to S3 -> store the record in DynamoDB -> enqueue
 * for async processing. Image goes first; a later DynamoDB failure leaves an orphaned object,
 * which is acceptable for this catalog (logged, cleaned up out of band). Enqueue is best-effort:
 * the product already exists, so a queue failure is logged but does not fail the request.
 */
export async function createProduct(body: string | undefined): Promise<Product> {
  const input = parseProductInput(body);
  const id = randomUUID();
  const now = new Date().toISOString();

  const imageUrl = await putProductImage(id, input.imageData);

  const product: Product = {
    id,
    name: input.name,
    description: input.description,
    price: input.price,
    imageUrl,
    createdAt: now,
    updatedAt: now,
  };

  await putProduct(product);

  try {
    await enqueueProductCreated(id);
  } catch (error) {
    console.error('Failed to enqueue product for processing', error);
  }

  return product;
}
