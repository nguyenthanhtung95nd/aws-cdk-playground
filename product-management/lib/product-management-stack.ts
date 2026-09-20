import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
import { DataConstruct } from './constructs/data-construct';
import { ProductImagesConstruct } from './constructs/product-images-construct';
import { ProcessingConstruct } from './constructs/processing-construct';
import { ProductsApiConstruct } from './constructs/products-api-construct';
import { WebHostingConstruct } from './constructs/web-hosting-construct';
import type { ProductNaming } from './utils';

export interface ProductManagementStackProps extends cdk.StackProps {
  tagSystem: string;
  tagEnvironment: string;
  tagSystemApp: string;
  tagCustomerCode: string;
}

/**
 * product-management - a single, atomically deployed stack combining S3, Lambda, HTTP API v2,
 * DynamoDB, SQS/DLQ, and Secrets Manager for one domain: Product. Composed from isolated L3
 * constructs; one CloudFormation unit means `cdk deploy`/`cdk destroy` are all-or-nothing, with
 * no cross-stack exports to strand resources.
 *
 * Currently composes: data (KMS + DynamoDB) + product-images (S3) + products-api (Lambda router
 * + HTTP API, GET/POST). Still to come: async processing (SQS + DLQ + consumer + Secret) and
 * web-hosting (S3 + CloudFront/OAC).
 */
export class ProductManagementStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props: ProductManagementStackProps) {
    super(scope, id, props);

    const naming: ProductNaming = {
      tagSystem: props.tagSystem,
      tagEnvironment: props.tagEnvironment,
      tagCustomerCode: props.tagCustomerCode,
    };
    // Production-only hardening branches off this flag inside each construct.
    const isProd = props.tagEnvironment.startsWith('prod');

    ////////////////
    // Constructs //
    ////////////////

    const data = new DataConstruct(this, 'data', { naming, isProd });
    const images = new ProductImagesConstruct(this, 'product-images', { naming, isProd });
    const processing = new ProcessingConstruct(this, 'processing', {
      naming,
      isProd,
      encryptionKey: data.key,
    });
    const api = new ProductsApiConstruct(this, 'products-api', {
      naming,
      isProd,
      table: data.table,
      imagesBucket: images.bucket,
      queue: processing.queue,
      encryptionKey: data.key,
    });
    const web = new WebHostingConstruct(this, 'web', { naming, isProd });

    /////////////
    // Outputs //
    /////////////

    new cdk.CfnOutput(this, 'output-site-url', { value: `https://${web.distribution.distributionDomainName}` });
    new cdk.CfnOutput(this, 'output-api-endpoint', { value: api.httpApi.apiEndpoint });
    new cdk.CfnOutput(this, 'output-products-table', { value: data.table.tableName });
    new cdk.CfnOutput(this, 'output-product-images-bucket', { value: images.bucket.bucketName });
    new cdk.CfnOutput(this, 'output-processing-queue', { value: processing.queue.queueUrl });

    //////////
    // Tags //
    //////////

    cdk.Tags.of(this).add('System', props.tagSystem);
    cdk.Tags.of(this).add('Environment', props.tagEnvironment);
    cdk.Tags.of(this).add('SystemApp', props.tagSystemApp);
    cdk.Tags.of(this).add('CustomerCode', props.tagCustomerCode);
  }
}
