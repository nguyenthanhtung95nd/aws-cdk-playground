using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Microsoft.Extensions.Logging;
using OrderPipeline.Contracts;
using OrderPipeline.Domain.Orders;
using OrderPipeline.Domain.Ports;
using OrderPipeline.Functions.Observability;

namespace OrderPipeline.Functions.Handlers;

public class OrderDispatcher(
    IOrderStore orderStore,
    IOrderWorkQueue workQueue,
    ILogger<OrderDispatcher> logger)
{
    private const string Inserted = "INSERT";

    [LambdaFunction]
    public async Task<StreamsEventResponse> DispatchPlacedOrders(DynamoDBEvent input, ILambdaContext context)
    {
        using var invocation = logger.BeginScope(LogScope.Invocation(context.AwsRequestId));
        using var deadline = LambdaDeadline.For(context);

        var failures = new List<StreamsEventResponse.BatchItemFailure>();

        foreach (var record in input.Records)
        {
            if (!TryReadPlacedOrderId(record, out var orderId))
            {
                continue;
            }

            using var correlation = logger.BeginScope(LogScope.Correlation(orderId));
            using var span = OrderActivity.Begin(orderId);

            var moved = await orderStore.TransitionAsync(
                orderId, OrderStatus.Pending, OrderStatus.Processing, deadline.Token);

            if (!moved.IsSuccess)
            {
                logger.LogError("Could not start processing order {OrderId}. {Reason}", orderId, moved.Error);
                failures.Add(new StreamsEventResponse.BatchItemFailure { ItemIdentifier = record.EventID });
                continue;
            }

            // Queued even when the write was refused, because a redelivery means the previous
            // attempt may have stopped between moving the order and queueing the work for it.
            var queued = await workQueue.EnqueueAsync(orderId, deadline.Token);

            if (!queued.IsSuccess)
            {
                logger.LogError("Could not queue work for order {OrderId}. {Reason}", orderId, queued.Error);
                failures.Add(new StreamsEventResponse.BatchItemFailure { ItemIdentifier = record.EventID });
                continue;
            }

            if (moved.Value == OrderTransition.Moved)
            {
                logger.LogInformation(
                    "Order {OrderId} is now being processed, queued as message {MessageId}.",
                    orderId,
                    queued.Value);
            }
        }

        return new StreamsEventResponse { BatchItemFailures = failures };
    }

    private static bool TryReadPlacedOrderId(DynamoDBEvent.DynamodbStreamRecord record, out string orderId)
    {
        orderId = string.Empty;

        if (record.EventName != Inserted || record.Dynamodb?.NewImage is not { } image)
        {
            return false;
        }

        if (!image.TryGetValue(OrderAttributes.OrderId, out var id) || string.IsNullOrEmpty(id.S))
        {
            return false;
        }

        orderId = id.S;
        return true;
    }
}
