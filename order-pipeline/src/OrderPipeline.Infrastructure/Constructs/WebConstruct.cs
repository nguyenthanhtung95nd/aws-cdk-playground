using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Assets;
using Amazon.CDK.AWS.S3.Deployment;
using Amazon.CDK.AWS.Apigatewayv2;
using Cdklabs.CdkNag;
using Constructs;

namespace OrderPipeline.Infrastructure.Constructs;

public sealed class WebConstructProps
{
    public required StageConfig Stage { get; init; }

    public required HttpApi HttpApi { get; init; }
}

public sealed class WebConstruct : Construct
{
    private const string FrontendProject = "frontend";

    private const string ApiPathPrefix = "/api";

    public Distribution Distribution { get; }

    public WebConstruct(Construct scope, string id, WebConstructProps props) : base(scope, id)
    {
        var bucket = new Bucket(this, "site", new BucketProps
        {
            BucketName = $"{props.Stage.StackId}-site",
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            EnforceSSL = true,
            Encryption = BucketEncryption.S3_MANAGED,
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true
        });

        NagSuppressions.AddResourceSuppressions(bucket, new[]
        {
            new NagPackSuppression
            {
                Id = "AwsSolutions-S1",
                Reason = "Access logging is off by choice. Every object here is a public build "
                       + "artefact served through CloudFront, so a second copy of the request log "
                       + "would record who read files that are meant to be read by everyone."
            }
        });

        Distribution = new Distribution(this, "distribution", new DistributionProps
        {
            Comment = props.Stage.StackId,
            DefaultRootObject = "index.html",
            DefaultBehavior = new BehaviorOptions
            {
                Origin = S3BucketOrigin.WithOriginAccessControl(bucket),
                ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS
            },
            AdditionalBehaviors = new Dictionary<string, IBehaviorOptions>
            {
                [$"{ApiPathPrefix}/*"] = ApiBehavior(props)
            },
            // A missing key behind Origin Access Control answers 403, never 404, because the
            // distribution is not allowed to list the bucket. Mapping 404 as well would swallow
            // the API's own 404 for an unknown route, and that one has to reach the caller.
            ErrorResponses = new[]
            {
                new ErrorResponse
                {
                    HttpStatus = 403,
                    ResponseHttpStatus = 200,
                    ResponsePagePath = "/index.html",
                    Ttl = Duration.Minutes(1)
                }
            }
        });

        NagSuppressions.AddResourceSuppressions(Distribution, new[]
        {
            new NagPackSuppression
            {
                Id = "AwsSolutions-CFR1",
                Reason = "The site is a demo with no licensing or export constraint, so there is "
                       + "no country whose visitors it would be correct to turn away."
            },
            new NagPackSuppression
            {
                Id = "AwsSolutions-CFR2",
                Reason = "A web firewall guards an application with something to lose. This one "
                       + "serves static files and a demo API over throwaway data."
            },
            new NagPackSuppression
            {
                Id = "AwsSolutions-CFR3",
                Reason = "Distribution access logging is off for the same reason as the bucket's: "
                       + "it would bill storage to record reads of deliberately public files."
            },
            new NagPackSuppression
            {
                Id = "AwsSolutions-CFR4",
                Reason = "The default CloudFront certificate is in use, and its minimum protocol "
                       + "is fixed by AWS. Choosing TLS 1.2 or above becomes possible only once a "
                       + "custom domain and certificate exist."
            }
        });

        Deploy(bucket);
    }

    // The site and the API answer on one domain, so the browser never makes a cross-origin call
    // and the built bundle carries no account-specific address. CloudFront strips the prefix back
    // off before the request reaches the gateway, which is what the dev server already does.
    private BehaviorOptions ApiBehavior(WebConstructProps props)
    {
        var stripPrefix = new Amazon.CDK.AWS.CloudFront.Function(this, "strip-api-prefix", new FunctionProps
        {
            FunctionName = $"{props.Stage.StackId}-strip-api-prefix",
            Runtime = FunctionRuntime.JS_2_0,
            Code = FunctionCode.FromInline($$"""
                function handler(event) {
                  var request = event.request;
                  var prefix = '{{ApiPathPrefix}}';
                  if (request.uri.indexOf(prefix) === 0) {
                    request.uri = request.uri.slice(prefix.length) || '/';
                  }
                  return request;
                }
                """)
        });

        return new BehaviorOptions
        {
            Origin = new HttpOrigin(
                $"{props.HttpApi.ApiId}.execute-api.{Stack.Of(this).Region}.amazonaws.com"),
            ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
            AllowedMethods = AllowedMethods.ALLOW_ALL,
            CachePolicy = CachePolicy.CACHING_DISABLED,
            OriginRequestPolicy = OriginRequestPolicy.ALL_VIEWER_EXCEPT_HOST_HEADER,
            FunctionAssociations = new[]
            {
                new FunctionAssociation
                {
                    Function = stripPrefix,
                    EventType = FunctionEventType.VIEWER_REQUEST
                }
            }
        };
    }

    private void Deploy(IBucket bucket)
    {
        _ = new BucketDeployment(this, "deployment", new BucketDeploymentProps
        {
            DestinationBucket = bucket,
            Sources = new[] { Source.Asset(FrontendProject, BuildOptions()) },
            Distribution = Distribution,
            DistributionPaths = new[] { "/*" }
        });

        SuppressCdkOwnedDeploymentHandler();
    }

    private static Amazon.CDK.AWS.S3.Assets.IAssetOptions BuildOptions() =>
        new Amazon.CDK.AWS.S3.Assets.AssetOptions
    {
        Exclude = new[] { "node_modules", "dist", "test-results", "playwright-report", "e2e" },
        Bundling = new BundlingOptions
        {
            Image = DockerImage.FromRegistry("public.ecr.aws/docker/library/node:22-alpine"),
            OutputType = BundlingOutput.NOT_ARCHIVED,
            Command = new[]
            {
                "/bin/sh",
                "-c",
                "cp -r /asset-input/. /tmp/build && cd /tmp/build && npm ci && npm run build "
                + "&& cp -r dist/. /asset-output/"
            }
        }
    };

    // The upload is done by a Lambda that CDK creates once per stack and owns entirely. Its
    // runtime, its managed policy and the breadth of its S3 permissions are all decided by CDK,
    // so the findings against it cannot be answered from here.
    private void SuppressCdkOwnedDeploymentHandler()
    {
        var stack = Stack.Of(this);
        var handler = $"{stack.StackName}/Custom::CDKBucketDeployment8693BB64968944B69AAFB0CC9EB8756C";

        NagSuppressions.AddResourceSuppressionsByPath(stack, $"{handler}/ServiceRole/Resource", new[]
        {
            new NagPackSuppression
            {
                Id = "AwsSolutions-IAM4",
                Reason = "CDK gives its own upload handler the AWS managed basic execution role.",
                AppliesTo = new[]
                {
                    "Policy::arn:<AWS::Partition>:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
                }
            }
        });

        NagSuppressions.AddResourceSuppressionsByPath(
            stack, $"{handler}/ServiceRole/DefaultPolicy/Resource", new[]
            {
                new NagPackSuppression
                {
                    Id = "AwsSolutions-IAM5",
                    Reason = "CDK's upload handler reads the asset bucket and writes the site "
                           + "bucket; the wildcards in that policy are written by CDK."
                }
            });

        NagSuppressions.AddResourceSuppressionsByPath(stack, $"{handler}/Resource", new[]
        {
            new NagPackSuppression
            {
                Id = "AwsSolutions-L1",
                Reason = "The runtime of CDK's upload handler is pinned by CDK, not by this stack."
            }
        });
    }
}
