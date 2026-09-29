using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.Logging;
using OrderPipeline.Functions.Observability;

namespace OrderPipeline.Functions.Handlers;

// This handler deliberately holds no store. An order under review is still travelling the main
// path, and the one guarantee this lane owes the system is that it cannot disturb that journey.
// Owning no way to write is a stronger promise than choosing not to.
public class OrderReviewer(ILogger<OrderReviewer> logger)
{
    [LambdaFunction]
    public SQSBatchResponse ReviewOrders(SQSEvent input, ILambdaContext context)
    {
        using var invocation = logger.BeginScope(LogScope.Invocation(context.AwsRequestId));

        var failures = new List<SQSBatchResponse.BatchItemFailure>();

        foreach (var message in input.Records)
        {
            var orderId = message.Body ?? string.Empty;

            if (orderId.Length == 0)
            {
                logger.LogError("Review message {MessageId} carries no order id.", message.MessageId);
                failures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = message.MessageId });
                continue;
            }

            using var correlation = logger.BeginScope(LogScope.Correlation(orderId));
            using var span = OrderActivity.Begin(orderId, message);

            logger.LogInformation("Order {OrderId} is high value and needs review.", orderId);
        }

        return new SQSBatchResponse { BatchItemFailures = failures };
    }
}
