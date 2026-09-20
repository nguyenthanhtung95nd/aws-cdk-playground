import { describe, it } from 'vitest';
import * as cdk from 'aws-cdk-lib';
import { Match, Template } from 'aws-cdk-lib/assertions';
import { DataConstruct } from '../../lib/constructs/data-construct';

const naming = { tagSystem: 'productmgmt', tagEnvironment: 'dev', tagCustomerCode: 'demo' };

function synth(isProd = false): Template {
  const stack = new cdk.Stack(new cdk.App(), 'test');
  new DataConstruct(stack, 'data', {
    naming: { ...naming, tagEnvironment: isProd ? 'prod' : 'dev' },
    isProd,
  });
  return Template.fromStack(stack);
}

describe('DataConstruct', () => {
  const dev = synth();
  const prod = synth(true);

  it('creates a KMS key with rotation enabled', () => {
    dev.hasResourceProperties('AWS::KMS::Key', { EnableKeyRotation: true });
  });

  it('creates a shared products table (PK id) with CMK encryption and on-demand billing', () => {
    dev.hasResourceProperties('AWS::DynamoDB::Table', {
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
      SSESpecification: Match.objectLike({ SSEEnabled: true, SSEType: 'KMS' }),
    });
  });

  it('enables deletion protection and point-in-time recovery in prod', () => {
    prod.hasResourceProperties('AWS::DynamoDB::Table', {
      DeletionProtectionEnabled: true,
      PointInTimeRecoverySpecification: { PointInTimeRecoveryEnabled: true },
    });
  });
});
