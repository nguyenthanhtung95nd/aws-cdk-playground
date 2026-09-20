import {
  SecretsManagerClient,
  GetSecretValueCommand,
} from '@aws-sdk/client-secrets-manager';

// Client is created once per container so connections are reused across warm invocations.
const client = new SecretsManagerClient({});

/** Fetches a secret's string value; throws when the secret is missing or empty. */
export async function fetchSecret(secretId: string): Promise<string> {
  const response = await client.send(new GetSecretValueCommand({ SecretId: secretId }));
  if (!response.SecretString) {
    throw new Error('Secret value is undefined');
  }
  return response.SecretString;
}
