// Thrown when the JWT is missing or the API rejects it (401). The UI reacts by prompting
// the user to sign in again, rather than showing a generic error.
export class SessionExpiredError extends Error {}

// Any other non-OK API response; carries a user-facing message.
export class ApiError extends Error {}
