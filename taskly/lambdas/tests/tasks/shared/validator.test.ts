import { describe, it, expect } from 'vitest';
import { parseTitle, parseJsonBody, parseStatus, parseUpdate } from '../../../src/tasks/shared/validator';
import { ValidationError } from '../../../src/tasks/shared/errors';

describe('validator', () => {
  it('accepts and trims a normal title', () => {
    expect(parseTitle('  Buy milk ')).toBe('Buy milk');
  });

  it('rejects empty, whitespace, or non-string titles', () => {
    expect(() => parseTitle('')).toThrow(ValidationError);
    expect(() => parseTitle('   ')).toThrow(ValidationError);
    expect(() => parseTitle(undefined)).toThrow(ValidationError);
  });

  it('accepts exactly 200 chars but rejects 201', () => {
    expect(parseTitle('a'.repeat(200))).toHaveLength(200);
    expect(() => parseTitle('a'.repeat(201))).toThrow(ValidationError);
  });

  it('parses valid JSON and treats an empty body as {}', () => {
    expect(parseJsonBody('{"title":"x"}')).toEqual({ title: 'x' });
    expect(parseJsonBody(undefined)).toEqual({});
  });

  it('rejects malformed JSON', () => {
    expect(() => parseJsonBody('{bad')).toThrow(ValidationError);
  });

  it('accepts valid statuses and rejects unknown ones', () => {
    expect(parseStatus('doing')).toBe('doing');
    expect(() => parseStatus('archived')).toThrow(ValidationError);
    expect(() => parseStatus(42)).toThrow(ValidationError);
  });

  it('parses a partial update with only the provided fields', () => {
    expect(parseUpdate(JSON.stringify({ status: 'done' }))).toEqual({ status: 'done' });
    expect(parseUpdate(JSON.stringify({ title: 'New' }))).toEqual({ title: 'New' });
  });

  it('rejects an update with no title and no status', () => {
    expect(() => parseUpdate(JSON.stringify({}))).toThrow(ValidationError);
    expect(() => parseUpdate(undefined)).toThrow(ValidationError);
  });
});
