using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AwsApigatewayv2Integrations;
using Cdklabs.CdkNag;
using Constructs;
using OrderPipeline.Contracts;
using HttpMethod = Amazon.CDK.AWS.Apigatewayv2.HttpMethod;

namespace OrderPipeline.Infrastructure.Constructs;

public sealed class ApiConstructProps
{
    public required StageConfig Stage { get; init; }

    public required IFunction GetHealth { get; init; }

    public required IFunction PlaceOrder { get; init; }

    public required IFunction ListOrders { get; init; }
}

/// <summary>
/// The HTTP API and the routes that lead to the three request-driven functions.
/// </summary>
public sealed class ApiConstruct : Construct
{
    public HttpApi HttpApi { get; }

    public ApiConstruct(Construct scope, string id, ApiConstructProps props) : base(scope, id)
    {
        HttpApi = new HttpApi(this, "http-api", new HttpApiProps
        {
            ApiName = $"{props.Stage.StackId}-api",
            CreateDefaultStage = true,
            CorsPreflight = new CorsPreflightOptions
            {
                AllowOrigins = new[] { "*" },
                AllowMethods = new[] { CorsHttpMethod.GET, CorsHttpMethod.POST },
                AllowHeaders = new[] { "Content-Type" }
            }
        });

        Route(ApiRoutes.Health, HttpMethod.GET, "get-health-integration", props.GetHealth);
        Route(ApiRoutes.Orders, HttpMethod.POST, "place-order-integration", props.PlaceOrder);
        Route(ApiRoutes.Orders, HttpMethod.GET, "list-orders-integration", props.ListOrders);

        NagSuppressions.AddResourceSuppressions(HttpApi, new[]
        {
            new NagPackSuppression
            {
                Id = "AwsSolutions-APIG1",
                Reason = "Access logging is off by choice. Every route is backed by a function "
                       + "whose own logs record each invocation, and this API carries no data "
                       + "that would make a gateway-level audit trail worth its storage cost."
            },
            new NagPackSuppression
            {
                Id = "AwsSolutions-APIG4",
                Reason = "The API is deliberately open. It exposes demo order data only, and "
                       + "adding an identity provider would obscure the asynchronous processing "
                       + "this system exists to demonstrate."
            }
        }, applyToChildren: true);
    }

    private void Route(string path, HttpMethod method, string integrationId, IFunction target) =>
        HttpApi.AddRoutes(new AddRoutesOptions
        {
            Path = path,
            Methods = new[] { method },
            Integration = new HttpLambdaIntegration(integrationId, target)
        });
}
