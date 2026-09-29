using System.Text.Json;
using Amazon.CDK.Assertions;

namespace OrderPipeline.Infrastructure.Tests.Constructs;

public class WebConstructTests
{
    [Fact]
    public void Synthesize_Always_KeepsTheSiteBucketPrivateAndDisposable()
    {
        var template = StackTemplate.SynthesizeWith();

        template.HasResourceProperties("AWS::S3::Bucket", Match.ObjectLike(new Dictionary<string, object>
        {
            ["BucketName"] = "orderpipeline-dev-demo-site",
            ["PublicAccessBlockConfiguration"] = Match.ObjectLike(new Dictionary<string, object>
            {
                ["BlockPublicAcls"] = true,
                ["BlockPublicPolicy"] = true,
                ["IgnorePublicAcls"] = true,
                ["RestrictPublicBuckets"] = true
            })
        }));

        template.HasResource("AWS::S3::Bucket", Match.ObjectLike(new Dictionary<string, object>
        {
            ["DeletionPolicy"] = "Delete"
        }));
    }

    [Fact]
    public void Synthesize_Always_ServesTheSiteOverHttpsStartingAtIndexHtml()
    {
        var config = DistributionConfig();

        Assert.Equal("index.html", config.GetProperty("DefaultRootObject").GetString());
        Assert.Equal(
            "redirect-to-https",
            config.GetProperty("DefaultCacheBehavior").GetProperty("ViewerProtocolPolicy").GetString());
    }

    [Fact]
    public void Synthesize_Always_SendsApiCallsToTheGatewayAndNotToTheBucket()
    {
        var config = DistributionConfig();
        var behavior = ApiBehavior(config);

        var gateway = config.GetProperty("Origins")
            .EnumerateArray()
            .Single(origin => origin.TryGetProperty("CustomOriginConfig", out _));

        Assert.Equal(gateway.GetProperty("Id").GetString(), behavior.GetProperty("TargetOriginId").GetString());
        Assert.Contains("execute-api", gateway.GetProperty("DomainName").ToString());
    }

    [Fact]
    public void Synthesize_Always_LetsTheApiReceiveWritesAndNeverCachesItsAnswers()
    {
        var behavior = ApiBehavior(DistributionConfig());

        var methods = behavior.GetProperty("AllowedMethods")
            .EnumerateArray()
            .Select(method => method.GetString())
            .ToList();

        Assert.Contains("POST", methods);
        // CachingDisabled is an AWS managed policy with a fixed identifier.
        Assert.Equal("4135ea2d-6df8-44a3-9df3-4b5a84be39ad", behavior.GetProperty("CachePolicyId").GetString());
    }

    [Fact]
    public void Synthesize_Always_StripsTheApiPrefixBeforeTheGatewaySeesIt()
    {
        var template = StackTemplate.SynthesizeWith();
        var behavior = ApiBehavior(DistributionConfig(template));

        Assert.Equal(
            "viewer-request",
            behavior.GetProperty("FunctionAssociations")[0].GetProperty("EventType").GetString());

        var code = Resource(template, "AWS::CloudFront::Function")
            .GetProperty("Properties")
            .GetProperty("FunctionCode")
            .GetString()!;

        Assert.Contains("'/api'", code);
        Assert.Contains("indexOf(prefix) === 0", code);
        Assert.Contains("slice(prefix.length)", code);
    }

    // A path that is not in the bucket has to come back as the app, but the API's own 404 for an
    // unknown route must reach the caller unchanged. Mapping both codes would erase the second.
    [Fact]
    public void Synthesize_Always_FallsBackToTheAppWithoutSwallowingTheApis404()
    {
        var codes = DistributionConfig()
            .GetProperty("CustomErrorResponses")
            .EnumerateArray()
            .Select(response => response.GetProperty("ErrorCode").GetInt32())
            .ToList();

        Assert.Equal([403], codes);
    }

    [Fact]
    public void Synthesize_Always_ClearsTheEdgeCacheSoADeployServesTheNewBuild()
    {
        var template = StackTemplate.SynthesizeWith();

        template.HasResourceProperties("Custom::CDKBucketDeployment", Match.ObjectLike(
            new Dictionary<string, object>
            {
                ["DistributionPaths"] = new[] { "/*" }
            }));
    }

    private static JsonElement DistributionConfig(Template? template = null) =>
        Resource(template ?? StackTemplate.SynthesizeWith(), "AWS::CloudFront::Distribution")
            .GetProperty("Properties")
            .GetProperty("DistributionConfig");

    private static JsonElement ApiBehavior(JsonElement config) =>
        config.GetProperty("CacheBehaviors")
            .EnumerateArray()
            .Single(behavior => behavior.GetProperty("PathPattern").GetString() == "/api/*");

    private static JsonElement Resource(Template template, string type) =>
        JsonSerializer.SerializeToElement(template.FindResources(type).Values.Single());
}
