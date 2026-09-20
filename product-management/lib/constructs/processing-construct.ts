import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
import type { IKey } from 'aws-cdk-lib/aws-kms';
import * as sqs from 'aws-cdk-lib/aws-sqs';
import * as secretsmanager from 'aws-cdk-lib/aws-secretsmanager';
import * as logs from 'aws-cdk-lib/aws-logs';
import { Runtime } from 'aws-cdk-lib/aws-lambda';
import { NodejsFunction } from 'aws-cdk-lib/aws-lambda-nodejs';
import { SqsEventSource } from 'aws-cdk-lib/aws-lambda-event-sources';
import { NagSuppressions } from 'cdk-nag';
import { join } from 'path';
import { resourceName, type ProductNaming } from '../utils';

export interface ProcessingConstructProps {
  /** Naming context used for every resource name in this construct. */
  readonly naming: ProductNaming;
  /** CMK used to encrypt the secret and the consumer's log group. */
  readonly encryptionKey: IKey;
  /**
   * Apply production hardening: RETAIN the secret on stack removal.
   * @default false
   */
  readonly isProd?: boolean;
}

// Consumer Lambda timeout; the queue's visibility timeout must be at least this long.
const CONSUMER_TIMEOUT_SECONDS = 30;
const BATCH_SIZE = 10;
const MAX_RECEIVE_COUNT = 3;

/**
 * Asynchronous processing: an SQS queue (with a dead-letter queue) feeds a consumer Lambda that
 * signs each product id with an HMAC keyed by a secret. Failures retry up to maxReceiveCount, then
 * land in the DLQ. The producer (products API) is granted send access separately.
 */
export class ProcessingConstruct extends Construct {
  /** Main work queue; producers send "product created" messages here. */
  public readonly queue: sqs.Queue;
  /** Dead-letter queue for messages that exhaust their retries. */
  public readonly deadLetterQueue: sqs.Queue;
  /** Secret holding the HMAC `encryptionKey`. */
  public readonly secret: secretsmanager.Secret;

  constructor(scope: Construct, id: string, props: ProcessingConstructProps) {
    super(scope, id);
    const isProd = props.isProd ?? false;

    this.deadLetterQueue = new sqs.Queue(this, 'dlq', {
      queueName: resourceName(props.naming, 'processing-dlq'),
      enforceSSL: true,
      removalPolicy: isProd ? cdk.RemovalPolicy.RETAIN : cdk.RemovalPolicy.DESTROY,
    });

    this.queue = new sqs.Queue(this, 'queue', {
      queueName: resourceName(props.naming, 'processing'),
      enforceSSL: true,
      // Must be >= the consumer's timeout so a message is not redelivered mid-processing.
      visibilityTimeout: cdk.Duration.seconds(CONSUMER_TIMEOUT_SECONDS * 6),
      deadLetterQueue: { queue: this.deadLetterQueue, maxReceiveCount: MAX_RECEIVE_COUNT },
      removalPolicy: isProd ? cdk.RemovalPolicy.RETAIN : cdk.RemovalPolicy.DESTROY,
    });

    this.secret = new secretsmanager.Secret(this, 'secret', {
      secretName: resourceName(props.naming, 'processing-key'),
      encryptionKey: props.encryptionKey,
      // Generate a random HMAC key so no secret material lives in source or config.
      generateSecretString: {
        secretStringTemplate: JSON.stringify({}),
        generateStringKey: 'encryptionKey',
      },
      removalPolicy: isProd ? cdk.RemovalPolicy.RETAIN : cdk.RemovalPolicy.DESTROY,
    });

    const logGroup = new logs.LogGroup(this, 'consumer-logs', {
      logGroupName: `/aws/lambda/${resourceName(props.naming, 'processing')}`,
      encryptionKey: props.encryptionKey,
      retention: logs.RetentionDays.ONE_MONTH,
      removalPolicy: cdk.RemovalPolicy.DESTROY,
    });

    const consumer = new NodejsFunction(this, 'consumer', {
      functionName: resourceName(props.naming, 'processing'),
      runtime: Runtime.NODEJS_22_X,
      entry: join(__dirname, '..', '..', 'lambdas', 'src', 'products', 'consumer.ts'),
      handler: 'handler',
      timeout: cdk.Duration.seconds(CONSUMER_TIMEOUT_SECONDS),
      logGroup,
      environment: { PROCESSING_SECRET_ID: this.secret.secretArn },
    });

    this.secret.grantRead(consumer);
    consumer.addEventSource(new SqsEventSource(this.queue, { batchSize: BATCH_SIZE }));

    NagSuppressions.addResourceSuppressions(
      consumer,
      [
        {
          id: 'AwsSolutions-IAM4',
          reason: 'Lambda uses the AWS-managed basic execution role for CloudWatch Logs.',
          appliesTo: ['Policy::arn:<AWS::Partition>:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole'],
        },
        {
          id: 'AwsSolutions-IAM5',
          reason: 'grantRead scopes actions to this secret; the wildcard is the secret version stage suffix.',
        },
        {
          id: 'AwsSolutions-L1',
          reason: 'Node.js 22 is the latest Lambda runtime; cdk-nag 2.38 has not yet been updated to recognize nodejs22.x as latest.',
        },
      ],
      true,
    );

    // The DLQ is itself a dead-letter target, so it does not need its own DLQ.
    NagSuppressions.addResourceSuppressions(this.deadLetterQueue, [
      { id: 'AwsSolutions-SQS3', reason: 'This queue is the dead-letter queue; it does not need a further DLQ.' },
    ]);

    NagSuppressions.addResourceSuppressions(this.secret, [
      { id: 'AwsSolutions-SMG4', reason: 'Automatic rotation not required for this demo HMAC key.' },
    ]);
  }
}
