import { describe, it } from 'vitest';
import * as cdk from 'aws-cdk-lib';
import * as sqs from 'aws-cdk-lib/aws-sqs';
import { Match, Template } from 'aws-cdk-lib/assertions';
import { DataConstruct } from '../../lib/constructs/data-construct';
import { ProductImagesConstruct } from '../../lib/constructs/product-images-construct';
import { ProductsApiConstruct } from '../../lib/constructs/products-api-construct';

const naming = { tagSystem: 'productmgmt', tagEnvironment: 'dev', tagCustomerCode: 'demo' };

function synth(): Template {
  const stack = new cdk.Stack(new cdk.App(), 'test');
  const data = new DataConstruct(stack, 'data', { naming });
  const images = new ProductImagesConstruct(stack, 'product-images', { naming });
  const queue = new sqs.Queue(stack, 'queue');
  new ProductsApiConstruct(stack, 'products-api', {
    naming,
    table: data.table,
    imagesBucket: images.bucket,
    queue,
    encryptionKey: data.key,
  });
  return Template.fromStack(stack);
}

describe('ProductsApiConstruct', () => {
  const template = synth();

  it('creates a Node.js 22 router Lambda with table + bucket + queue env', () => {
    template.hasResourceProperties('AWS::Lambda::Function', {
      Runtime: 'nodejs22.x',
      Environment: {
        Variables: Match.objectLike({
          PRODUCTS_TABLE_NAME: Match.anyValue(),
          PRODUCT_IMAGES_BUCKET_NAME: Match.anyValue(),
          PRODUCTS_QUEUE_URL: Match.anyValue(),
        }),
      },
    });
  });

  it('creates an HTTP API v2', () => {
    template.hasResourceProperties('AWS::ApiGatewayV2::Api', { ProtocolType: 'HTTP' });
  });

  it('wires GET/POST /products and DELETE /products/{id} routes', () => {
    template.hasResourceProperties('AWS::ApiGatewayV2::Route', { RouteKey: 'GET /products' });
    template.hasResourceProperties('AWS::ApiGatewayV2::Route', { RouteKey: 'POST /products' });
    template.hasResourceProperties('AWS::ApiGatewayV2::Route', { RouteKey: 'DELETE /products/{id}' });
  });
});
