import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
import type { ITable } from 'aws-cdk-lib/aws-dynamodb';
import type { IBucket } from 'aws-cdk-lib/aws-s3';
import type { IKey } from 'aws-cdk-lib/aws-kms';
import type { IQueue } from 'aws-cdk-lib/aws-sqs';
import * as logs from 'aws-cdk-lib/aws-logs';
import * as apigwv2 from 'aws-cdk-lib/aws-apigatewayv2';
import { HttpLambdaIntegration } from 'aws-cdk-lib/aws-apigatewayv2-integrations';
import { Runtime } from 'aws-cdk-lib/aws-lambda';
import { NodejsFunction } from 'aws-cdk-lib/aws-lambda-nodejs';
import { NagSuppressions } from 'cdk-nag';
import { join } from 'path';
import { resourceName, type ProductNaming } from '../utils';

export interface ProductsApiConstructProps {
  /** Naming context used for every resource name in this construct. */
  readonly naming: ProductNaming;
  /** Table the router Lambda reads and writes (granted least-privilege). */
  readonly table: ITable;
  /** Public-read image bucket the router Lambda uploads to. */
  readonly imagesBucket: IBucket;
  /** Queue the router Lambda sends "product created" messages to (granted send-only). */
  readonly queue: IQueue;
  /** CMK used to encrypt the Lambda log group. */
  readonly encryptionKey: IKey;
  /**
   * Apply production hardening: API access logging.
   * @default false
   */
  readonly isProd?: boolean;
}

/**
 * The `/products` HTTP API: a single router Lambda (Node 22) behind an HTTP API v2. One function
 * routes every method. The API is intentionally open (no auth); hygiene comes from
 * least-privilege IAM, KMS, and cdk-nag. DELETE is added later.
 */
export class ProductsApiConstruct extends Construct {
  /** The HTTP API; expose its endpoint via a stack output. */
  public readonly httpApi: apigwv2.HttpApi;

  constructor(scope: Construct, id: string, props: ProductsApiConstructProps) {
    super(scope, id);
    const isProd = props.isProd ?? false;

    const logGroup = new logs.LogGroup(this, 'fn-logs', {
      logGroupName: `/aws/lambda/${resourceName(props.naming, 'products')}`,
      encryptionKey: props.encryptionKey,
      retention: logs.RetentionDays.ONE_MONTH,
      removalPolicy: cdk.RemovalPolicy.DESTROY,
    });

    const fn = new NodejsFunction(this, 'fn', {
      functionName: resourceName(props.naming, 'products'),
      runtime: Runtime.NODEJS_22_X,
      entry: join(__dirname, '..', '..', 'lambdas', 'src', 'products', 'handler.ts'),
      handler: 'handler',
      // Base64 image upload + S3 put can be slower than a plain CRUD call.
      timeout: cdk.Duration.seconds(30),
      logGroup,
      environment: {
        PRODUCTS_TABLE_NAME: props.table.tableName,
        PRODUCT_IMAGES_BUCKET_NAME: props.imagesBucket.bucketName,
        PRODUCTS_QUEUE_URL: props.queue.queueUrl,
      },
    });

    // Least privilege: read/write the table, put/delete image objects, send-only on the queue.
    props.table.grantReadWriteData(fn);
    props.imagesBucket.grantPut(fn);
    props.imagesBucket.grantDelete(fn);
    props.queue.grantSendMessages(fn);

    NagSuppressions.addResourceSuppressions(
      fn,
      [
        {
          id: 'AwsSolutions-IAM4',
          reason: 'Lambda uses the AWS-managed basic execution role for CloudWatch Logs.',
          appliesTo: ['Policy::arn:<AWS::Partition>:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole'],
        },
        {
          id: 'AwsSolutions-IAM5',
          reason: 'grant* helpers scope actions to the products table and image bucket; wildcards are the object/index ARNs.',
        },
        {
          id: 'AwsSolutions-L1',
          reason: 'Node.js 22 is the latest Lambda runtime; cdk-nag 2.38 has not yet been updated to recognize nodejs22.x as latest.',
        },
      ],
      true,
    );

    this.httpApi = new apigwv2.HttpApi(this, 'http-api', {
      apiName: resourceName(props.naming, 'api'),
      corsPreflight: {
        allowOrigins: ['*'],
        allowMethods: [
          apigwv2.CorsHttpMethod.GET,
          apigwv2.CorsHttpMethod.POST,
          apigwv2.CorsHttpMethod.DELETE,
        ],
        allowHeaders: ['Content-Type', 'Authorization'],
      },
    });

    const integration = new HttpLambdaIntegration('integration', fn);

    // Collection routes: list + create.
    this.httpApi.addRoutes({
      path: '/products',
      methods: [apigwv2.HttpMethod.GET, apigwv2.HttpMethod.POST],
      integration,
    });

    // Item route: delete a single product by id.
    this.httpApi.addRoutes({
      path: '/products/{id}',
      methods: [apigwv2.HttpMethod.DELETE],
      integration,
    });

    // The API is intentionally open (no auth). Hygiene comes from least-privilege IAM, KMS,
    // and cdk-nag on everything else.
    NagSuppressions.addResourceSuppressions(
      this.httpApi,
      [
        {
          id: 'AwsSolutions-APIG4',
          reason: 'The API is intentionally unauthenticated; auth is out of scope for this project.',
        },
      ],
      true,
    );

    if (!isProd) {
      NagSuppressions.addResourceSuppressions(
        this.httpApi,
        [{ id: 'AwsSolutions-APIG1', reason: 'Access logging only required for production.' }],
        true,
      );
    }
  }
}
