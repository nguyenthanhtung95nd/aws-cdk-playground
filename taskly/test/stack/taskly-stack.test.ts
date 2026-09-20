import { describe, it } from 'vitest';
import * as cdk from 'aws-cdk-lib';
import { Template } from 'aws-cdk-lib/assertions';
import { TasklyStack } from '../../lib/taskly-stack';

function synth(): Template {
  const stack = new TasklyStack(new cdk.App(), 'taskly-dev-demo', {
    tagSystem: 'taskly',
    tagEnvironment: 'dev',
    tagSystemApp: 'todo',
    tagCustomerCode: 'demo',
  });
  return Template.fromStack(stack);
}

describe('TasklyStack (composition)', () => {
  const template = synth();

  it('wires all four concerns into one stack', () => {
    template.resourceCountIs('AWS::DynamoDB::Table', 1);
    template.resourceCountIs('AWS::Cognito::UserPool', 1);
    template.resourceCountIs('AWS::ApiGatewayV2::Api', 1);
    template.resourceCountIs('AWS::ApiGatewayV2::Route', 4);
    template.resourceCountIs('AWS::CloudFront::Distribution', 1);
  });

  it('propagates the four tags to every resource', () => {
    // CloudFormation renders tags sorted alphabetically by key.
    template.hasResourceProperties('AWS::DynamoDB::Table', {
      Tags: [
        { Key: 'CustomerCode', Value: 'demo' },
        { Key: 'Environment', Value: 'dev' },
        { Key: 'System', Value: 'taskly' },
        { Key: 'SystemApp', Value: 'todo' },
      ],
    });
  });

  it('exports the endpoints the frontend and operators need', () => {
    template.hasOutput('outputapiendpoint', {});
    template.hasOutput('outputuserpoolid', {});
    template.hasOutput('outputsiteurl', {});
  });
});
