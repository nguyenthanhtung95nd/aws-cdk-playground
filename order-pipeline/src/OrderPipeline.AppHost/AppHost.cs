using Aspire.Hosting.AWS.DynamoDB;
using Aspire.Hosting.AWS.Lambda;
using OrderPipeline.AppHost;
using OrderPipeline.AppHost.Commands;
using OrderPipeline.Contracts;
using OrderPipeline.Infrastructure;

const int ApiGatewayPort = 5300;

// Playwright's baseURL points here; keep the two in step.
const int WebPort = 5173;

var builder = DistributedApplication.CreateBuilder(args);

if (builder.ExecutionContext.IsPublishMode)
{
    throw new InvalidOperationException("The AppHost is a local run model only. Deploy with cdk deploy.");
}

var cdkJsonPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "cdk.json"));

var localStage = StageConfig.FromCdkJson(cdkJsonPath, "dev") with { TagEnvironment = "local" };

var ordersTableName = $"{localStage.StackId}-orders";

// Without SharedDb, DynamoDB Local keeps one database per access key and the functions would
// never see the table this host creates.
var dynamo = builder.AddAWSDynamoDBLocal("dynamodb", new DynamoDBLocalOptions { SharedDb = true })
    .WithResetOrdersCommand(ordersTableName);

builder.AddDynamoDbAdmin(dynamo, localStage.Region);

builder.Eventing.Subscribe<ResourceReadyEvent>(dynamo.Resource, async (_, cancellationToken) =>
{
    var endpoint = dynamo.Resource.GetEndpoints().Single();
    var endpointUrl = await endpoint.GetValueAsync(cancellationToken)
        ?? throw new InvalidOperationException("DynamoDB Local is ready but has no endpoint to reach it at.");

    await LocalOrdersTable.EnsureAsync(endpointUrl, ordersTableName, cancellationToken);
});

var getHealth = AddFunction(LambdaHandlers.GetHealth, needsOrders: false);
var placeOrder = AddFunction(LambdaHandlers.PlaceOrder, needsOrders: true);
var listOrders = AddFunction(LambdaHandlers.ListOrders, needsOrders: true);

// The SQS lanes (dispatcher, worker, settler, reviewer) have no local SQS to run against.

var apiGateway = builder.AddAWSAPIGatewayEmulator("ApiGateway", APIGatewayType.HttpV2)
    // Fixed, unproxied port: the Vite dev server proxies /api to it.
    .WithHttpEndpoint(port: ApiGatewayPort, name: "http", isProxied: false)
    .WithHttpHealthCheck(ApiRoutes.Health)
    .WithReference(getHealth, Method.Get, ApiRoutes.Health)
    .WithReference(placeOrder, Method.Post, ApiRoutes.Orders)
    .WithReference(listOrders, Method.Get, ApiRoutes.Orders)
    .WithSeedOrdersCommand();

builder.AddViteApp("web", "../../frontend")
    .WithEndpoint("http", endpoint => endpoint.Port = WebPort)
    .WithEnvironment("API_PROXY_TARGET", apiGateway.GetEndpoint("http"))
    .WithEnvironment("VITE_API_BASE_URL", "/api")
    .WaitFor(apiGateway)
    .WithExternalHttpEndpoints();

builder.Build().Run();

IResourceBuilder<LambdaProjectResource> AddFunction(string handler, bool needsOrders)
{
    // The AWS integration does not turn the exporter on for Lambda resources the way AddProject does.
    var function = builder
        .AddAWSLambdaFunction<Projects.OrderPipeline_Functions>(handler, LambdaHandlers.Assembly)
        .WithOtlpExporter()
        .WithEnvironment(EnvironmentVariables.Handler, handler);

    if (!needsOrders)
    {
        return function;
    }

    return function
        .WithReference(dynamo)
        .WaitFor(dynamo)
        .WithEnvironment(EnvironmentVariables.OrdersTableName, ordersTableName)
        .WithEnvironment(EnvironmentVariables.HighValueThreshold, localStage.HighValueThreshold)
        .WithEnvironment(EnvironmentVariables.BusinessLimit, localStage.BusinessLimit)
        .WithEnvironment(AwsSdkVariables.Region, localStage.Region)
        .WithEnvironment(AwsSdkVariables.AccessKeyId, AwsSdkVariables.LocalCredential)
        .WithEnvironment(AwsSdkVariables.SecretAccessKey, AwsSdkVariables.LocalCredential);
}
