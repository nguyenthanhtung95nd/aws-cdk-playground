import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
import * as s3 from 'aws-cdk-lib/aws-s3';
import { NagSuppressions } from 'cdk-nag';
import { resourceName, type ProductNaming } from '../utils';

export interface ProductImagesConstructProps {
  /** Naming context used for every resource name in this construct. */
  readonly naming: ProductNaming;
  /**
   * Apply production hardening: RETAIN the bucket on stack removal.
   * @default false
   */
  readonly isProd?: boolean;
}

/**
 * S3 bucket for product images. Deliberately PUBLIC-READ: images are served directly to browsers
 * without auth. This is the one intentional deviation from the BLOCK_ALL default - the SPA hosting
 * bucket stays private.
 */
export class ProductImagesConstruct extends Construct {
  /** Public-read bucket holding `products/{id}.{ext}` objects. */
  public readonly bucket: s3.Bucket;

  constructor(scope: Construct, id: string, props: ProductImagesConstructProps) {
    super(scope, id);
    const isProd = props.isProd ?? false;

    this.bucket = new s3.Bucket(this, 'bucket', {
      bucketName: resourceName(props.naming, 'product-images'),
      // Public-read image serving. Requires opening BlockPublicAccess for the public policy.
      publicReadAccess: true,
      blockPublicAccess: new s3.BlockPublicAccess({
        blockPublicAcls: false,
        blockPublicPolicy: false,
        ignorePublicAcls: false,
        restrictPublicBuckets: false,
      }),
      enforceSSL: true,
      encryption: s3.BucketEncryption.S3_MANAGED,
      cors: [
        {
          allowedMethods: [s3.HttpMethods.GET],
          allowedOrigins: ['*'],
          allowedHeaders: ['*'],
        },
      ],
      removalPolicy: isProd ? cdk.RemovalPolicy.RETAIN : cdk.RemovalPolicy.DESTROY,
      autoDeleteObjects: !isProd,
    });

    NagSuppressions.addResourceSuppressions(this.bucket, [
      {
        id: 'AwsSolutions-S1',
        reason: 'Server access logging not required for the demo product-images bucket.',
      },
      {
        id: 'AwsSolutions-S2',
        reason: 'Public read is intentional - product images are served directly to browsers.',
      },
      {
        id: 'AwsSolutions-S5',
        reason: 'Public read is intentional - product images are served directly to browsers.',
      },
    ]);
  }
}
