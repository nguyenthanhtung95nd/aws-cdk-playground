using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using OrderPipeline.Contracts;
using OrderPipeline.Functions.Responses;

namespace OrderPipeline.Functions.Handlers;

public class HealthApi
{
    private const string ServiceName = "order-pipeline";

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, ApiRoutes.Health)]
    public IHttpResult GetHealth() =>
        HttpResults.Ok(new HealthResponse(ServiceName, "ok"));
}
