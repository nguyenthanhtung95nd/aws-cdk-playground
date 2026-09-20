// Domain errors mapped to HTTP status codes in responses.ts. Throwing these lets the
// handler stay a thin router: business code signals intent, the boundary maps to HTTP.
export class ValidationError extends Error {}
export class NotFoundError extends Error {}
export class UnauthorizedError extends Error {}
