using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.Logging;
using OrderPipeline.Domain.Orders;
using OrderPipeline.Domain.Ports;
using OrderPipeline.Functions.Observability;

namespace OrderPipeline.Functions.Handlers;

public class OrderFailureSettler(IOrderStore orderStore, ILogger<OrderFailureSettler> logger)
{
    private const string ParkedReason =
        "The order spent its whole retry budget without being processed, so it was parked.";

    [LambdaFunction]
    public async Task<SQSBatchResponse> SettleFailures(SQSEvent input, ILambdaContext context)
    {
        using var invocation = logger.BeginScope(LogScope.Invocation(context.AwsRequestId));
        using var deadline = LambdaDeadline.For(context);

        var failures = new List<SQSBatchResponse.BatchItemFailure>();

        foreach (var message in input.Records)
        {
            var orderId = message.Body ?? string.Empty;

            if (orderId.Length == 0)
            {
                logger.LogError("Parked message {MessageId} carries no order id.", message.MessageId);
                failures.Add(RetryOf(message));
                continue;
            }

            using var correlation = logger.BeginScope(LogScope.Correlation(orderId));
            using var span = OrderActivity.Begin(orderId, message);

            var settled = await orderStore.FailAsync(
                orderId,
                OrderStatus.Processing,
                new OrderFailure(FailureKind.Technical, ParkedReason),
                deadline.Token);

            if (!settled.IsSuccess)
            {
                logger.LogError("Could not settle parked order {OrderId}. {Reason}", orderId, settled.Error);
                failures.Add(RetryOf(message));
                continue;
            }

            // An order that settled itself while its message sat in the parking area is the
            // expected race, not a fault, so the refused write is left unremarked.
            if (settled.Value == OrderTransition.Moved)
            {
                logger.LogInformation("Order {OrderId} was settled as failed. {Reason}", orderId, ParkedReason);
            }
        }

        return new SQSBatchResponse { BatchItemFailures = failures };
    }

    private static SQSBatchResponse.BatchItemFailure RetryOf(SQSEvent.SQSMessage message) =>
        new() { ItemIdentifier = message.MessageId };
}
