import { describe, it } from 'vitest';
import * as cdk from 'aws-cdk-lib';
import { Template } from 'aws-cdk-lib/assertions';
import { WebHostingConstruct } from '../../lib/constructs/web-hosting-construct';

const naming = { tagSystem: 'productmgmt', tagEnvironment: 'dev', tagCustomerCode: 'demo' };

function synth(): Template {
  const stack = new cdk.Stack(new cdk.App(), 'test');
  new WebHostingConstruct(stack, 'web', { naming });
  return Template.fromStack(stack);
}

describe('WebHostingConstruct', () => {
  const template = synth();

  it('creates a private SPA bucket (all public access blocked)', () => {
    template.hasResourceProperties('AWS::S3::Bucket', {
      PublicAccessBlockConfiguration: {
        BlockPublicAcls: true,
        BlockPublicPolicy: true,
        IgnorePublicAcls: true,
        RestrictPublicBuckets: true,
      },
    });
  });

  it('serves the bucket through a CloudFront distribution with SPA fallback', () => {
    template.resourceCountIs('AWS::CloudFront::Distribution', 1);
    template.hasResourceProperties('AWS::CloudFront::Distribution', {
      DistributionConfig: {
        DefaultRootObject: 'index.html',
      },
    });
  });
});
