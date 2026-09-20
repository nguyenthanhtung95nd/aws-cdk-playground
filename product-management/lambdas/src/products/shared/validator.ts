import { ProductInput } from '../model/product';
import { ValidationError } from './errors';

const IMAGE_DATA_URI = /^data:image\/(png|jpe?g|gif);base64,/;
// API Gateway/Lambda cap the request payload at ~6MB; base64 inflates the raw bytes ~33%,
// so guard the encoded string length up front instead of letting the gateway truncate silently.
const MAX_IMAGE_DATA_LENGTH = 6 * 1024 * 1024;

/** Parses a request body into an object; malformed JSON is a client error, not a crash. */
export function parseJsonBody(body: string | undefined): Record<string, unknown> {
  if (!body) return {};
  try {
    return JSON.parse(body) as Record<string, unknown>;
  } catch {
    throw new ValidationError('Invalid request body');
  }
}

/** Validates the create payload; all four fields are required. */
export function parseProductInput(body: string | undefined): ProductInput {
  const input = parseJsonBody(body);
  const { name, description, price, imageData } = input;

  if (
    typeof name !== 'string' ||
    name.trim().length === 0 ||
    typeof description !== 'string' ||
    description.trim().length === 0 ||
    typeof price !== 'number' ||
    typeof imageData !== 'string'
  ) {
    throw new ValidationError('All fields required: name, description, price and image');
  }

  if (!IMAGE_DATA_URI.test(imageData)) {
    throw new ValidationError('image must be a base64 data URI (png, jpeg or gif)');
  }

  if (imageData.length > MAX_IMAGE_DATA_LENGTH) {
    throw new ValidationError('Image too large (max ~6MB)');
  }

  return { name: name.trim(), description: description.trim(), price, imageData };
}
