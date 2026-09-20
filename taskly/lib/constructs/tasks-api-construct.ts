import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
import type { ITable } from 'aws-cdk-lib/aws-dynamodb';
import type { IKey } from 'aws-cdk-lib/aws-kms';
import type { IUserPool, IUserPoolClient } from 'aws-cdk-lib/aws-cognito';
import * as logs from 'aws-cdk-lib/aws-logs';
import * as apigwv2 from 'aws-cdk-lib/aws-apigatewayv2';
import { HttpUserPoolAuthorizer } from 'aws-cdk-lib/aws-apigatewayv2-authorizers';
import { HttpLambdaIntegration } from 'aws-cdk-lib/aws-apigatewayv2-integrations';
import { Runtime } from 'aws-cdk-lib/aws-lambda';
import { NodejsFunction } from 'aws-cdk-lib/aws-lambda-nodejs';
import { NagSuppressions } from 'cdk-nag';
import { join } from 'path';
import { resourceName, type TasklyNaming } from '../utils';

export interface TasksApiConstructProps {
  /** Naming context used for every resource name in this construct. */
  readonly naming: TasklyNaming;
  /** Table the CRUD Lambda reads and writes (granted least-privilege). */
  readonly table: ITable;
  /** User pool backing the JWT authorizer. */
  readonly userPool: IUserPool;
  /** App client trusted by the JWT authorizer. */
  readonly userPoolClient: IUserPoolClient;
  /** CMK used to encrypt the Lambda log group. */
  readonly encryptionKey: IKey;
  /**
   * Apply production hardening: API access logging.
   * @default false
   */
  readonly isProd?: boolean;
}

/**
 * The `/tasks` HTTP API: a single CRUD Lambda (Node 22) behind an HTTP API v2 with a Cognito
 * JWT authorizer. One function routes every method; all four routes require a valid token.
 */
export class TasksApiConstruct extends Construct {
  /** The HTTP API; expose its endpoint via a stack output. */
  public readonly httpApi: apigwv2.HttpApi;

  constructor(scope: Construct, id: string, props: TasksApiConstructProps) {
    super(scope, id);
    const isProd = props.isProd ?? false;

    const logGroup = new logs.LogGroup(this, 'fn-logs', {
      logGroupName: `/aws/lambda/${resourceName(props.naming, 'tasks')}`,
      encryptionKey: props.encryptionKey,
      retention: logs.RetentionDays.ONE_MONTH,
      removalPolicy: cdk.RemovalPolicy.DESTROY,
    });

    const fn = new NodejsFunction(this, 'fn', {
      functionName: resourceName(props.naming, 'tasks'),
      runtime: Runtime.NODEJS_22_X,
      entry: join(__dirname, '..', '..', 'lambdas', 'src', 'tasks', 'handler.ts'),
      handler: 'handler',
      timeout: cdk.Duration.seconds(10),
      logGroup,
      environment: { TABLE_NAME: props.table.tableName },
    });

    // Least privilege: the function only needs read/write on the tasks table.
    props.table.grantReadWriteData(fn);

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
          reason: 'grantReadWriteData scopes actions to the tasks table; the wildcard is the table index ARN.',
        },
        {
          id: 'AwsSolutions-L1',
          reason: 'Node.js 22 is the latest Lambda runtime; cdk-nag 2.38 has not yet been updated to recognize nodejs22.x as latest.',
        },
      ],
      true,
    );

    const authorizer = new HttpUserPoolAuthorizer('authorizer', props.userPool, {
      userPoolClients: [props.userPoolClient],
    });

    this.httpApi = new apigwv2.HttpApi(this, 'http-api', {
      apiName: resourceName(props.naming, 'api'),
      corsPreflight: {
        allowOrigins: ['*'],
        allowMethods: [
          apigwv2.CorsHttpMethod.GET,
          apigwv2.CorsHttpMethod.POST,
          apigwv2.CorsHttpMethod.PUT,
          apigwv2.CorsHttpMethod.DELETE,
        ],
        allowHeaders: ['Content-Type', 'Authorization'],
      },
    });

    const integration = new HttpLambdaIntegration('integration', fn);

    // Collection routes: list + create.
    this.httpApi.addRoutes({
      path: '/tasks',
      methods: [apigwv2.HttpMethod.GET, apigwv2.HttpMethod.POST],
      integration,
      authorizer,
    });

    // Item routes: edit + delete a single task by id.
    this.httpApi.addRoutes({
      path: '/tasks/{id}',
      methods: [apigwv2.HttpMethod.PUT, apigwv2.HttpMethod.DELETE],
      integration,
      authorizer,
    });

    if (!isProd) {
      NagSuppressions.addResourceSuppressions(
        this.httpApi,
        [{ id: 'AwsSolutions-APIG1', reason: 'Access logging only required for production.' }],
        true,
      );
    }
  }
}
