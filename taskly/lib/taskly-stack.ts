import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
import { DataConstruct } from './constructs/data-construct';
import { AuthConstruct } from './constructs/auth-construct';
import { TasksApiConstruct } from './constructs/tasks-api-construct';
import { WebHostingConstruct } from './constructs/web-hosting-construct';
import type { TasklyNaming } from './utils';

export interface TasklyStackProps extends cdk.StackProps {
  tagSystem: string;
  tagEnvironment: string;
  tagSystemApp: string;
  tagCustomerCode: string;
}

/**
 * Taskly - a single, atomically deployed stack composed from four isolated L3 constructs.
 * One stack means one CloudFormation unit: `cdk deploy` and `cdk destroy` are all-or-nothing,
 * with no cross-stack exports to strand resources. Data is protected at the resource level
 * (RETAIN + deletion protection in prod), not by splitting into a separate stateful stack.
 */
export class TasklyStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props: TasklyStackProps) {
    super(scope, id, props);

    const naming: TasklyNaming = {
      tagSystem: props.tagSystem,
      tagEnvironment: props.tagEnvironment,
      tagCustomerCode: props.tagCustomerCode,
    };
    // Production-only hardening branches off this flag inside each construct.
    const isProd = props.tagEnvironment.startsWith('prod');

    // Compose the blocks. Cross-block wiring is explicit and type-checked via each Props interface.
    const data = new DataConstruct(this, 'data', { naming, isProd });
    const auth = new AuthConstruct(this, 'auth', { naming, isProd });
    const api = new TasksApiConstruct(this, 'api', {
      naming,
      isProd,
      table: data.table,
      userPool: auth.userPool,
      userPoolClient: auth.userPoolClient,
      encryptionKey: data.key,
    });
    const web = new WebHostingConstruct(this, 'web', { naming, isProd });

    /////////////
    // Outputs //
    /////////////

    new cdk.CfnOutput(this, 'output-site-url', { value: `https://${web.distribution.distributionDomainName}` });
    new cdk.CfnOutput(this, 'output-api-endpoint', { value: api.httpApi.apiEndpoint });
    new cdk.CfnOutput(this, 'output-user-pool-id', { value: auth.userPool.userPoolId });
    new cdk.CfnOutput(this, 'output-user-pool-client-id', { value: auth.userPoolClient.userPoolClientId });
    new cdk.CfnOutput(this, 'output-tasks-table', { value: data.table.tableName });

    //////////
    // Tags //
    //////////

    cdk.Tags.of(this).add('System', props.tagSystem);
    cdk.Tags.of(this).add('Environment', props.tagEnvironment);
    cdk.Tags.of(this).add('SystemApp', props.tagSystemApp);
    cdk.Tags.of(this).add('CustomerCode', props.tagCustomerCode);
  }
}
