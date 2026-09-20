import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
import * as s3 from 'aws-cdk-lib/aws-s3';
import * as s3deploy from 'aws-cdk-lib/aws-s3-deployment';
import * as cloudfront from 'aws-cdk-lib/aws-cloudfront';
import * as origins from 'aws-cdk-lib/aws-cloudfront-origins';
import { NagSuppressions } from 'cdk-nag';
import { existsSync } from 'fs';
import { join } from 'path';
import { resourceName, type ProductNaming } from '../utils';

export interface WebHostingConstructProps {
  /** Naming context used for every resource name in this construct. */
  readonly naming: ProductNaming;
  /**
   * Apply production hardening: RETAIN the site bucket on stack removal.
   * @default false
   */
  readonly isProd?: boolean;
}

/**
 * Static web hosting: a private S3 bucket served through CloudFront via Origin Access Control.
 * The built SPA is uploaded when `frontend/dist` exists; otherwise synth still succeeds with a
 * warning, so the stack can be deployed before the first frontend build.
 */
export class WebHostingConstruct extends Construct {
  /** The CloudFront distribution; expose its domain via a stack output. */
  public readonly distribution: cloudfront.Distribution;

  constructor(scope: Construct, id: string, props: WebHostingConstructProps) {
    super(scope, id);
    const isProd = props.isProd ?? false;

    const bucket = new s3.Bucket(this, 'bucket', {
      bucketName: resourceName(props.naming, 'site'),
      blockPublicAccess: s3.BlockPublicAccess.BLOCK_ALL,
      enforceSSL: true,
      encryption: s3.BucketEncryption.S3_MANAGED,
      removalPolicy: isProd ? cdk.RemovalPolicy.RETAIN : cdk.RemovalPolicy.DESTROY,
      autoDeleteObjects: !isProd,
    });
    NagSuppressions.addResourceSuppressions(bucket, [
      { id: 'AwsSolutions-S1', reason: 'Server access logging not required for the demo static-site bucket.' },
    ]);

    this.distribution = new cloudfront.Distribution(this, 'distribution', {
      defaultRootObject: 'index.html',
      defaultBehavior: {
        origin: origins.S3BucketOrigin.withOriginAccessControl(bucket),
        viewerProtocolPolicy: cloudfront.ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
      },
      // SPA fallback: client-side routes and missing keys resolve to index.html.
      errorResponses: [
        { httpStatus: 403, responseHttpStatus: 200, responsePagePath: '/index.html', ttl: cdk.Duration.minutes(1) },
        { httpStatus: 404, responseHttpStatus: 200, responsePagePath: '/index.html', ttl: cdk.Duration.minutes(1) },
      ],
    });
    NagSuppressions.addResourceSuppressions(this.distribution, [
      { id: 'AwsSolutions-CFR1', reason: 'Geo restrictions not required for this demo.' },
      { id: 'AwsSolutions-CFR2', reason: 'WAF not required for this demo static site.' },
      { id: 'AwsSolutions-CFR3', reason: 'Access logging only required for production.' },
      { id: 'AwsSolutions-CFR4', reason: 'Uses the default CloudFront domain and cert; a TLS1.2+ custom cert is added with a custom domain.' },
    ]);

    const uiDir = join(__dirname, '..', '..', 'frontend', 'dist');
    if (existsSync(uiDir)) {
      new s3deploy.BucketDeployment(this, 'deployment', {
        destinationBucket: bucket,
        sources: [s3deploy.Source.asset(uiDir)],
        distribution: this.distribution,
        distributionPaths: ['/*'],
      });

      // The BucketDeployment handler (Lambda + role) is a stack-level singleton owned by CDK;
      // its managed policy, S3 wildcards, and runtime are not configurable from here.
      const stack = cdk.Stack.of(this);
      const bd = `${stack.stackName}/Custom::CDKBucketDeployment8693BB64968944B69AAFB0CC9EB8756C`;
      NagSuppressions.addResourceSuppressionsByPath(stack, `${bd}/ServiceRole/Resource`, [
        {
          id: 'AwsSolutions-IAM4',
          reason: 'CDK-managed BucketDeployment handler uses the AWS-managed basic execution role.',
          appliesTo: ['Policy::arn:<AWS::Partition>:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole'],
        },
      ]);
      NagSuppressions.addResourceSuppressionsByPath(stack, `${bd}/ServiceRole/DefaultPolicy/Resource`, [
        {
          id: 'AwsSolutions-IAM5',
          reason: 'CDK-managed handler needs S3 read on the CDK asset bucket and read/write on the site bucket to copy the build; scope is owned by CDK.',
        },
      ]);
      NagSuppressions.addResourceSuppressionsByPath(stack, `${bd}/Resource`, [
        {
          id: 'AwsSolutions-L1',
          reason: 'Runtime of the CDK-managed BucketDeployment handler is owned by CDK and not configurable here.',
        },
      ]);
    } else {
      cdk.Annotations.of(this).addWarning(
        `Frontend build not found at ${uiDir} - run "npm run build" in frontend/ before deploy.`,
      );
    }
  }
}
