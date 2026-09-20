import { APIGatewayProxyEventV2WithJWTAuthorizer } from 'aws-lambda';
import { UnauthorizedError } from './errors';

/**
 * Extracts the caller's user id from the validated JWT.
 * Cognito's `sub` claim is the stable per-user identifier; we use it as the DynamoDB
 * partition key so every operation is scoped to the caller.
 */
export function getUserId(event: APIGatewayProxyEventV2WithJWTAuthorizer): string {
  const sub = event.requestContext.authorizer?.jwt?.claims?.sub;
  if (typeof sub !== 'string' || sub.length === 0) {
    throw new UnauthorizedError('Missing user identity in token.');
  }
  return sub;
}
