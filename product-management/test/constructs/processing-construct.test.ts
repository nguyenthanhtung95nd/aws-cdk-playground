import { describe, it } from 'vitest';
import * as cdk from 'aws-cdk-lib';
import * as kms from 'aws-cdk-lib/aws-kms';
import { Match, Template } from 'aws-cdk-lib/assertions';
import { ProcessingConstruct } from '../../lib/constructs/processing-construct';

const naming = { tagSystem: 'productmgmt', tagEnvironment: 'dev', tagCustomerCode: 'demo' };

function synth(): Template {
  const stack = new cdk.Stack(new cdk.App(), 'test');
  const key = new kms.Key(stack, 'key');
  new ProcessingConstruct(stack, 'processing', { naming, encryptionKey: key });
  return Template.fromStack(stack);
}

describe('ProcessingConstruct', () => {
  const template = synth();

  it('creates a main queue with a DLQ redrive (maxReceiveCount 3)', () => {
    template.resourceCountIs('AWS::SQS::Queue', 2);
    template.hasResourceProperties('AWS::SQS::Queue', {
      RedrivePolicy: Match.objectLike({ maxReceiveCount: 3 }),
    });
  });

  it('creates a secret for the HMAC key', () => {
    template.resourceCountIs('AWS::SecretsManager::Secret', 1);
  });

  it('creates a Node.js 22 consumer Lambda triggered by the queue', () => {
    template.hasResourceProperties('AWS::Lambda::Function', {
      Runtime: 'nodejs22.x',
      Environment: { Variables: Match.objectLike({ PROCESSING_SECRET_ID: Match.anyValue() }) },
    });
    template.resourceCountIs('AWS::Lambda::EventSourceMapping', 1);
  });
});
