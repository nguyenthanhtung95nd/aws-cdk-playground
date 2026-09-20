import { describe, it } from 'vitest';
import * as cdk from 'aws-cdk-lib';
import { Match, Template } from 'aws-cdk-lib/assertions';
import * as kms from 'aws-cdk-lib/aws-kms';
import * as dynamodb from 'aws-cdk-lib/aws-dynamodb';
import * as cognito from 'aws-cdk-lib/aws-cognito';
import { TasksApiConstruct } from '../../lib/constructs/tasks-api-construct';

const naming = { tagSystem: 'taskly', tagEnvironment: 'dev', tagCustomerCode: 'demo' };

function synth(): Template {
  const stack = new cdk.Stack(new cdk.App(), 'test');
  // Minimal real dependencies, so this construct is tested in isolation.
  const key = new kms.Key(stack, 'key');
  const table = new dynamodb.Table(stack, 'table', {
    partitionKey: { name: 'userId', type: dynamodb.AttributeType.STRING },
    sortKey: { name: 'id', type: dynamodb.AttributeType.STRING },
  });
  const userPool = new cognito.UserPool(stack, 'pool');
  const userPoolClient = userPool.addClient('client');

  new TasksApiConstruct(stack, 'api', { naming, table, userPool, userPoolClient, encryptionKey: key });
  return Template.fromStack(stack);
}

describe('TasksApiConstruct', () => {
  const template = synth();

  it('runs the CRUD Lambda on Node 22 with the table name in its environment', () => {
    template.hasResourceProperties('AWS::Lambda::Function', {
      Runtime: 'nodejs22.x',
      Environment: { Variables: Match.objectLike({ TABLE_NAME: Match.anyValue() }) },
    });
  });

  it('exposes an HTTP API with a JWT authorizer and four routes', () => {
    template.resourceCountIs('AWS::ApiGatewayV2::Api', 1);
    template.hasResourceProperties('AWS::ApiGatewayV2::Authorizer', { AuthorizerType: 'JWT' });
    template.resourceCountIs('AWS::ApiGatewayV2::Route', 4);
  });
});
