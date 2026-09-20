import { APIGatewayProxyStructuredResultV2 } from 'aws-lambda';
import { NotFoundError, UnauthorizedError, ValidationError } from './errors';

const CORS_HEADERS = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Headers': 'Content-Type,Authorization',
  'Access-Control-Allow-Methods': 'GET,POST,PUT,DELETE,OPTIONS',
};

export function json(statusCode: number, body: unknown): APIGatewayProxyStructuredResultV2 {
  return {
    statusCode,
    headers: { 'Content-Type': 'application/json', ...CORS_HEADERS },
    body: JSON.stringify(body),
  };
}

/** 204 No Content - used by DELETE, which has nothing to return. */
export function noContent(): APIGatewayProxyStructuredResultV2 {
  return { statusCode: 204, headers: { ...CORS_HEADERS } };
}

/** Maps a thrown domain error to the right HTTP status; unknown errors become a safe 500. */
export function toErrorResponse(error: unknown): APIGatewayProxyStructuredResultV2 {
  if (error instanceof ValidationError) return json(400, { message: error.message });
  if (error instanceof NotFoundError) return json(404, { message: error.message });
  if (error instanceof UnauthorizedError) return json(401, { message: error.message });
  // Never leak internal details to the client - log server-side, return a generic message.
  console.error('Unhandled error', error);
  return json(500, { message: 'Internal server error' });
}
