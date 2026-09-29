using Amazon.CDK.Assertions;

namespace OrderPipeline.Infrastructure.Tests.Constructs;

public class ApiConstructTests
{
    [Fact]
    public void Synthesize_Always_ExposesHealthAndBothOrderOperations()
    {
        var template = StackTemplate.SynthesizeWith();

        foreach (var route in new[] { "GET /health", "POST /orders", "GET /orders" })
        {
            template.HasResourceProperties("AWS::ApiGatewayV2::Route", new Dictionary<string, object>
            {
                ["RouteKey"] = route
            });
        }
    }

    [Fact]
    public void Synthesize_Always_AllowsTheBrowserToCallTheApiFromAnotherOrigin()
    {
        var template = StackTemplate.SynthesizeWith();

        template.HasResourceProperties("AWS::ApiGatewayV2::Api", Match.ObjectLike(new Dictionary<string, object>
        {
            ["CorsConfiguration"] = Match.ObjectLike(new Dictionary<string, object>
            {
                ["AllowOrigins"] = new[] { "*" },
                ["AllowMethods"] = new[] { "GET", "POST" }
            })
        }));
    }
}
