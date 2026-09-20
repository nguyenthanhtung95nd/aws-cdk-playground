// Shared configuration for the local harness (server + worker) against LocalStack.
export const LOCALSTACK_ENDPOINT = 'http://localhost:4566';
export const REGION = 'us-west-1';
export const TABLE_NAME = 'productmgmt-local-products';
export const IMAGES_BUCKET = 'productmgmt-local-product-images';
export const QUEUE_NAME = 'productmgmt-local-processing';
export const DLQ_NAME = 'productmgmt-local-processing-dlq';
export const SECRET_ID = 'productmgmt-local-processing-key';

/** Points the AWS SDK v3 at LocalStack with dummy credentials. Call before importing handlers. */
export function seedLocalEnv(): void {
  process.env.AWS_ENDPOINT_URL = LOCALSTACK_ENDPOINT;
  process.env.AWS_ACCESS_KEY_ID = 'local';
  process.env.AWS_SECRET_ACCESS_KEY = 'local';
  process.env.AWS_REGION = REGION;
}
