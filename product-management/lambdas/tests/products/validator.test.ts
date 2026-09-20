import { describe, it, expect } from 'vitest';
import { parseProductInput } from '../../src/products/shared/validator';
import { ValidationError } from '../../src/products/shared/errors';

const VALID_IMAGE = 'data:image/png;base64,aGVsbG8=';

function bodyOf(overrides: Record<string, unknown> = {}): string {
  return JSON.stringify({
    name: 'Shirt',
    description: 'A nice shirt',
    price: 19.99,
    imageData: VALID_IMAGE,
    ...overrides,
  });
}

describe('parseProductInput', () => {
  it('accepts a valid payload and trims name/description', () => {
    const input = parseProductInput(bodyOf({ name: '  Shirt  ', description: '  nice  ' }));
    expect(input).toEqual({ name: 'Shirt', description: 'nice', price: 19.99, imageData: VALID_IMAGE });
  });

  it('rejects a missing name', () => {
    expect(() => parseProductInput(bodyOf({ name: '' }))).toThrow(ValidationError);
  });

  it('rejects a non-numeric price', () => {
    expect(() => parseProductInput(bodyOf({ price: '10' }))).toThrow(ValidationError);
  });

  it('rejects a missing image', () => {
    expect(() => parseProductInput(JSON.stringify({ name: 'x', description: 'y', price: 1 }))).toThrow(
      ValidationError,
    );
  });

  it('rejects a non-data-URI image', () => {
    expect(() => parseProductInput(bodyOf({ imageData: 'plain-string' }))).toThrow(ValidationError);
  });

  it('rejects an oversized image', () => {
    const huge = 'data:image/png;base64,' + 'a'.repeat(6 * 1024 * 1024 + 1);
    expect(() => parseProductInput(bodyOf({ imageData: huge }))).toThrow(/too large/i);
  });

  it('rejects malformed JSON', () => {
    expect(() => parseProductInput('{not json')).toThrow(ValidationError);
  });
});
