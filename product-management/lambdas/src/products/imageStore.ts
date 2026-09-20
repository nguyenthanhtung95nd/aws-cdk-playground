import { S3Client, PutObjectCommand, DeleteObjectCommand } from '@aws-sdk/client-s3';

// LocalStack needs path-style addressing; real S3 uses virtual-hosted style (the default).
const usingLocalEndpoint = Boolean(process.env.AWS_ENDPOINT_URL);
const s3 = new S3Client(usingLocalEndpoint ? { forcePathStyle: true } : {});
const BUCKET = process.env.PRODUCT_IMAGES_BUCKET_NAME as string;

const EXTENSION_BY_MIME: Record<string, string> = {
  'image/png': 'png',
  'image/jpeg': 'jpg',
  'image/jpg': 'jpg',
  'image/gif': 'gif',
};

/**
 * Uploads a base64 data-URI image to S3 under `products/{id}.{ext}` and returns its public URL.
 * Base64-over-JSON upload; the bucket is public-read.
 */
export async function putProductImage(productId: string, imageData: string): Promise<string> {
  const mimeMatch = imageData.match(/^data:(image\/[a-z]+);base64,/);
  const mime = mimeMatch?.[1] ?? 'image/jpeg';
  const extension = EXTENSION_BY_MIME[mime] ?? 'jpg';
  const base64 = imageData.replace(/^data:image\/[a-z]+;base64,/, '');
  const buffer = Buffer.from(base64, 'base64');
  const key = `products/${productId}.${extension}`;

  await s3.send(
    new PutObjectCommand({ Bucket: BUCKET, Key: key, Body: buffer, ContentType: mime }),
  );

  return buildImageUrl(key);
}

/** Deletes the object behind a stored imageUrl. Key is derived from the URL, not the raw path. */
export async function deleteProductImage(imageUrl: string): Promise<void> {
  const key = imageKeyFromUrl(imageUrl);
  if (!key) return;
  await s3.send(new DeleteObjectCommand({ Bucket: BUCKET, Key: key }));
}

/** Region-aware virtual-hosted URL; path-style when on LocalStack. */
function buildImageUrl(key: string): string {
  const endpoint = process.env.AWS_ENDPOINT_URL;
  if (endpoint) return `${endpoint.replace(/\/$/, '')}/${BUCKET}/${key}`;
  const region = process.env.AWS_REGION ?? 'us-west-1';
  return `https://${BUCKET}.s3.${region}.amazonaws.com/${key}`;
}

/**
 * Recovers the S3 key from a stored imageUrl. Keys always look like `products/{id}.{ext}`, so the
 * last two path segments are the key regardless of virtual-hosted vs path-style URL shape.
 */
function imageKeyFromUrl(imageUrl: string): string | undefined {
  const segments = imageUrl.split('/').filter(Boolean);
  if (segments.length < 2) return undefined;
  return segments.slice(-2).join('/');
}
