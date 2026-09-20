import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
import * as kms from 'aws-cdk-lib/aws-kms';
import * as iam from 'aws-cdk-lib/aws-iam';
import * as dynamodb from 'aws-cdk-lib/aws-dynamodb';
import { NagSuppressions } from 'cdk-nag';
import { resourceName, type ProductNaming } from '../utils';

export interface DataConstructProps {
  /** Naming context used for every resource name in this construct. */
  readonly naming: ProductNaming;
  /**
   * Apply production hardening: point-in-time recovery, RETAIN, and deletion protection.
   * @default false
   */
  readonly isProd?: boolean;
}

/**
 * Stateful data layer: a customer-managed KMS key and the products table. The catalog is shared,
 * so the partition key is the product `id` (not per-user). Encapsulating both here guarantees the
 * table is always CMK-encrypted.
 */
export class DataConstruct extends Construct {
  /** CMK that encrypts the table; reused by other constructs for log-group encryption. */
  public readonly key: kms.Key;
  /** Shared products table (PK `id`). */
  public readonly table: dynamodb.Table;

  constructor(scope: Construct, id: string, props: DataConstructProps) {
    super(scope, id);
    const isProd = props.isProd ?? false;

    this.key = new kms.Key(this, 'key', {
      alias: resourceName(props.naming, 'key'),
      description: `product-management CMK (${props.naming.tagEnvironment})`,
      enableKeyRotation: true,
      removalPolicy: isProd ? cdk.RemovalPolicy.RETAIN : cdk.RemovalPolicy.DESTROY,
    });

    // CloudWatch Logs can only use a CMK if the key policy allows the logs service principal.
    this.key.grantEncryptDecrypt(new iam.ServicePrincipal('logs.amazonaws.com'));

    this.table = new dynamodb.Table(this, 'table', {
      tableName: resourceName(props.naming, 'products'),
      partitionKey: { name: 'id', type: dynamodb.AttributeType.STRING },
      billingMode: dynamodb.BillingMode.PAY_PER_REQUEST,
      encryption: dynamodb.TableEncryption.CUSTOMER_MANAGED,
      encryptionKey: this.key,
      pointInTimeRecoverySpecification: isProd ? { pointInTimeRecoveryEnabled: true } : undefined,
      // Data protection lives at the resource level, so the whole app stays a single
      // atomic deploy/destroy unit (no separate stateful stack needed).
      deletionProtection: isProd,
      removalPolicy: isProd ? cdk.RemovalPolicy.RETAIN : cdk.RemovalPolicy.DESTROY,
    });

    if (!isProd) {
      NagSuppressions.addResourceSuppressions(this.table, [
        { id: 'AwsSolutions-DDB3', reason: 'Point-in-time recovery only required for production.' },
      ]);
    }
  }
}
