import { describe, it } from 'vitest';
import * as cdk from 'aws-cdk-lib';
import { Match, Template } from 'aws-cdk-lib/assertions';
import { ProductImagesConstruct } from '../../lib/constructs/product-images-construct';

const naming = { tagSystem: 'productmgmt', tagEnvironment: 'dev', tagCustomerCode: 'demo' };

function synth(): Template {
  const stack = new cdk.Stack(new cdk.App(), 'test');
  new ProductImagesConstruct(stack, 'product-images', { naming });
  return Template.fromStack(stack);
}

describe('ProductImagesConstruct', () => {
  const template = synth();

  it('creates a bucket with public access NOT blocked (intentional)', () => {
    template.hasResourceProperties('AWS::S3::Bucket', {
      PublicAccessBlockConfiguration: {
        BlockPublicAcls: false,
        BlockPublicPolicy: false,
        IgnorePublicAcls: false,
        RestrictPublicBuckets: false,
      },
    });
  });

  it('attaches a public-read bucket policy allowing s3:GetObject', () => {
    template.hasResourceProperties('AWS::S3::BucketPolicy', {
      PolicyDocument: Match.objectLike({
        Statement: Match.arrayWith([
          Match.objectLike({ Action: 's3:GetObject', Effect: 'Allow', Principal: Match.anyValue() }),
        ]),
      }),
    });
  });
});
