using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.Logging;
using OrderPipeline.Domain.Common;
using OrderPipeline.Domain.Orders;
using OrderPipeline.Domain.Ports;
using OrderPipeline.Functions.Observability;

namespace OrderPipeline.Functions.Handlers;

public class OrderWorker(
    IOrderStore orderStore,
    OrderThresholds thresholds,
    ILogger<OrderWorker> logger)
{
    [LambdaFunction]
    public async Task<SQSBatchResponse> CompleteOrders(SQSEvent input, ILambdaContext context)
    {
        using var invocation = logger.BeginScope(LogScope.Invocation(context.AwsRequestId));
        using var deadline = LambdaDeadline.For(context);

        var failures = new List<SQSBatchResponse.BatchItemFailure>();

        foreach (var message in input.Records)
        {
            var orderId = message.Body ?? string.Empty;

            if (orderId.Length == 0)
            {
                logger.LogError("Message {MessageId} carries no order id.", message.MessageId);
                failures.Add(RetryOf(message));
                continue;
            }

            using var correlation = logger.BeginScope(LogScope.Correlation(orderId));
            using var span = OrderActivity.Begin(orderId, message);

            var order = await orderStore.GetAsync(orderId, deadline.Token);

            if (!order.IsSuccess)
            {
                logger.LogError("Could not read order {OrderId}. {Reason}", orderId, order.Error);
                failures.Add(RetryOf(message));
                continue;
            }

            if (BusinessLimitRule.RejectionOf(order.Value!, thresholds) is { } rejection)
            {
                if (!(await Reject(orderId, rejection, deadline.Token)).IsSuccess)
                {
                    failures.Add(RetryOf(message));
                }

                continue;
            }

            if (FailureProbe.IsRequestedBy(order.Value!))
            {
                logger.LogError("Order {OrderId} failed. {Reason}", orderId, FailureProbe.Reason);
                failures.Add(RetryOf(message));
                continue;
            }

            if (!(await Complete(orderId, deadline.Token)).IsSuccess)
            {
                failures.Add(RetryOf(message));
            }
        }

        return new SQSBatchResponse { BatchItemFailures = failures };
    }

    private async Task<Result<OrderTransition>> Complete(string orderId, CancellationToken cancellationToken)
    {
        var completed = await orderStore.TransitionAsync(
            orderId, OrderStatus.Processing, OrderStatus.Completed, cancellationToken);

        if (!completed.IsSuccess)
        {
            logger.LogError("Could not complete order {OrderId}. {Reason}", orderId, completed.Error);
        }
        else if (completed.Value == OrderTransition.Moved)
        {
            logger.LogInformation("Order {OrderId} is complete.", orderId);
        }

        return completed;
    }

    // A rejected order is a settled order, not a failed delivery, so the message is finished with
    // and nothing here asks for it back.
    private async Task<Result<OrderTransition>> Reject(
        string orderId,
        OrderFailure rejection,
        CancellationToken cancellationToken)
    {
        var rejected = await orderStore.FailAsync(
            orderId, OrderStatus.Processing, rejection, cancellationToken);

        if (!rejected.IsSuccess)
        {
            logger.LogError("Could not reject order {OrderId}. {Reason}", orderId, rejected.Error);
        }
        else if (rejected.Value == OrderTransition.Moved)
        {
            logger.LogInformation("Order {OrderId} was rejected. {Reason}", orderId, rejection.Reason);
        }

        return rejected;
    }

    private static SQSBatchResponse.BatchItemFailure RetryOf(SQSEvent.SQSMessage message) =>
        new() { ItemIdentifier = message.MessageId };
}
