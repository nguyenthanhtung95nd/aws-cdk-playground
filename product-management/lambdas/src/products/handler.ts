import { APIGatewayProxyEventV2, APIGatewayProxyStructuredResultV2 } from 'aws-lambda';
import { createProduct } from './createProduct';
import { listProducts } from './listProducts';
import { deleteProduct } from './deleteProduct';
import { ValidationError } from './shared/errors';
import { json, toErrorResponse } from './shared/responses';

/**
 * Single entry point for /products and /products/{id}. One Lambda routes every method by switching
 * on the HTTP verb; each branch delegates to a focused operation. The API is intentionally open.
 */
export async function handler(
  event: APIGatewayProxyEventV2,
): Promise<APIGatewayProxyStructuredResultV2> {
  try {
    switch (event.requestContext.http.method) {
      case 'GET':
        return json(200, await listProducts());
      case 'POST':
        return json(201, await createProduct(event.body));
      case 'DELETE':
        await deleteProduct(requireId(event));
        return json(200, { message: 'Product deleted' });
      default:
        return json(405, { message: `Method ${event.requestContext.http.method} not allowed` });
    }
  } catch (error) {
    return toErrorResponse(error);
  }
}

/** The {id} routes always supply a path parameter; a missing one is a malformed request. */
function requireId(event: APIGatewayProxyEventV2): string {
  const id = event.pathParameters?.id;
  if (!id) {
    throw new ValidationError('Product id is required');
  }
  return id;
}
