import { describe, it } from 'vitest';
import * as cdk from 'aws-cdk-lib';
import { Template } from 'aws-cdk-lib/assertions';
import { ProductManagementStack } from '../../lib/product-management-stack';

function synth(): Template {
  const app = new cdk.App();
  const stack = new ProductManagementStack(app, 'productmgmt-dev-demo', {
    env: { account: '111111111111', region: 'us-west-1' },
    tagSystem: 'productmgmt',
    tagEnvironment: 'dev',
    tagSystemApp: 'product',
    tagCustomerCode: 'demo',
  });
  return Template.fromStack(stack);
}

describe('ProductManagementStack composition', () => {
  const template = synth();

  it('composes the data, images, processing, API, and web layers', () => {
    template.resourceCountIs('AWS::DynamoDB::Table', 1);
    template.resourceCountIs('AWS::KMS::Key', 1);
    template.resourceCountIs('AWS::S3::Bucket', 2); // product images + SPA site
    template.resourceCountIs('AWS::ApiGatewayV2::Api', 1);
    template.resourceCountIs('AWS::SQS::Queue', 2); // work queue + DLQ
    template.resourceCountIs('AWS::SecretsManager::Secret', 1);
    template.resourceCountIs('AWS::CloudFront::Distribution', 1);
  });

  it('exposes the site URL, API endpoint, table, image bucket, and queue as outputs', () => {
    // CDK strips hyphens from CfnOutput logical IDs (output-api-endpoint -> outputapiendpoint).
    const keys = Object.keys(template.findOutputs('*')).join(',').toLowerCase();
    if (!keys.includes('siteurl')) throw new Error('missing site url output');
    if (!keys.includes('apiendpoint')) throw new Error('missing api endpoint output');
    if (!keys.includes('productstable')) throw new Error('missing table output');
    if (!keys.includes('productimagesbucket')) throw new Error('missing bucket output');
    if (!keys.includes('processingqueue')) throw new Error('missing processing queue output');
  });
});
