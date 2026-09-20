import { APIGatewayProxyEventV2WithJWTAuthorizer, APIGatewayProxyStructuredResultV2 } from 'aws-lambda';
import { createTask } from './createTask';
import { deleteTask } from './deleteTask';
import { listTasks } from './listTasks';
import { updateTask } from './updateTask';
import { getUserId } from './shared/auth';
import { ValidationError } from './shared/errors';
import { json, noContent, toErrorResponse } from './shared/responses';

/**
 * Single entry point for /tasks and /tasks/{id}. One Lambda routes every method by switching
 * on the HTTP verb; each branch delegates to a focused operation. All work is scoped to the
 * caller's userId (from the validated JWT), so users never touch each other's data.
 */
export async function handler(
  event: APIGatewayProxyEventV2WithJWTAuthorizer,
): Promise<APIGatewayProxyStructuredResultV2> {
  try {
    const userId = getUserId(event);

    switch (event.requestContext.http.method) {
      case 'GET':
        return json(200, await listTasks(userId));
      case 'POST':
        return json(201, await createTask(userId, event.body));
      case 'PUT':
        return json(200, await updateTask(userId, requireId(event), event.body));
      case 'DELETE':
        await deleteTask(userId, requireId(event));
        return noContent();
      default:
        return json(405, { message: `Method ${event.requestContext.http.method} not allowed` });
    }
  } catch (error) {
    return toErrorResponse(error);
  }
}

/** The {id} routes always supply a path parameter; a missing one is a malformed request. */
function requireId(event: APIGatewayProxyEventV2WithJWTAuthorizer): string {
  const id = event.pathParameters?.id;
  if (!id) {
    throw new ValidationError('Task id is required');
  }
  return id;
}
