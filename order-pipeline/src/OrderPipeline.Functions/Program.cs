using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using OrderPipeline.Contracts;
using OrderPipeline.Functions.Handlers;
using OrderPipeline.ServiceDefaults;

var requested = Environment.GetEnvironmentVariable(EnvironmentVariables.Handler) ?? "<unset>";
var serializer = new CamelCaseLambdaJsonSerializer();

Func<Stream, ILambdaContext, Task<Stream>> handler;

switch (requested)
{
    case LambdaHandlers.GetHealth:
        var health = new HealthApi_GetHealth_Generated();
        handler = (input, context) => Task.FromResult(health.GetHealth(Request(input), context));
        break;

    case LambdaHandlers.PlaceOrder:
        var placeOrder = new OrderApi_PlaceOrder_Generated();
        handler = (input, context) => placeOrder.PlaceOrder(Request(input), context);
        break;

    case LambdaHandlers.ListOrders:
        var listOrders = new OrderApi_ListOrders_Generated();
        handler = (input, context) => listOrders.ListOrders(Request(input), context);
        break;

    case LambdaHandlers.DispatchPlacedOrders:
        var dispatcher = new OrderDispatcher_DispatchPlacedOrders_Generated();
        handler = Event<DynamoDBEvent, StreamsEventResponse>(dispatcher.DispatchPlacedOrders);
        break;

    case LambdaHandlers.CompleteOrders:
        var worker = new OrderWorker_CompleteOrders_Generated();
        handler = Event<SQSEvent, SQSBatchResponse>(worker.CompleteOrders);
        break;

    case LambdaHandlers.SettleFailures:
        var settler = new OrderFailureSettler_SettleFailures_Generated();
        handler = Event<SQSEvent, SQSBatchResponse>(settler.SettleFailures);
        break;

    case LambdaHandlers.ReviewOrders:
        var reviewer = new OrderReviewer_ReviewOrders_Generated();
        handler = Event<SQSEvent, SQSBatchResponse>(
            (input, lambdaContext) => Task.FromResult(reviewer.ReviewOrders(input, lambdaContext)));
        break;

    default:
        throw new InvalidOperationException(
            $"'{EnvironmentVariables.Handler}' was '{requested}'. "
            + $"Set it to one of: {string.Join(", ", LambdaHandlers.All)}.");
}

using var telemetry = LambdaTelemetry.Start(requested);

await LambdaBootstrapBuilder
    .Create(Traced(handler))
    .Build()
    .RunAsync();

Func<Stream, ILambdaContext, Task<Stream>> Traced(Func<Stream, ILambdaContext, Task<Stream>> inner) =>
    (input, context) => telemetry.TraceAsync(inner, input, context);

APIGatewayHttpApiV2ProxyRequest Request(Stream input) =>
    serializer.Deserialize<APIGatewayHttpApiV2ProxyRequest>(input);

Func<Stream, ILambdaContext, Task<Stream>> Event<TEvent, TResponse>(
    Func<TEvent, ILambdaContext, Task<TResponse>> invoke) =>
    async (input, context) =>
    {
        var response = await invoke(serializer.Deserialize<TEvent>(input), context);

        var payload = new MemoryStream();
        serializer.Serialize(response, payload);
        payload.Position = 0;
        return payload;
    };
