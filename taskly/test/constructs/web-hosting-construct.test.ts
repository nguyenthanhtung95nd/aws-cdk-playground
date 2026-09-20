import { describe, it } from 'vitest';
import * as cdk from 'aws-cdk-lib';
import { Template } from 'aws-cdk-lib/assertions';
import { WebHostingConstruct } from '../../lib/constructs/web-hosting-construct';

const naming = { tagSystem: 'taskly', tagEnvironment: 'dev', tagCustomerCode: 'demo' };

function synth(): Template {
  const stack = new cdk.Stack(new cdk.App(), 'test');
  new WebHostingConstruct(stack, 'web', { naming });
  return Template.fromStack(stack);
}

describe('WebHostingConstruct', () => {
  const template = synth();

  it('creates a fully private (block-all) S3 bucket', () => {
    template.hasResourceProperties('AWS::S3::Bucket', {
      PublicAccessBlockConfiguration: {
        BlockPublicAcls: true,
        BlockPublicPolicy: true,
        IgnorePublicAcls: true,
        RestrictPublicBuckets: true,
      },
    });
  });

  it('serves the bucket through a CloudFront distribution via Origin Access Control', () => {
    template.resourceCountIs('AWS::CloudFront::Distribution', 1);
    template.resourceCountIs('AWS::CloudFront::OriginAccessControl', 1);
  });
});
