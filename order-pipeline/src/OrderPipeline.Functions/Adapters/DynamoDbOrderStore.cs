using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using OrderPipeline.Contracts;
using OrderPipeline.Domain.Common;
using OrderPipeline.Domain.Orders;
using OrderPipeline.Domain.Ports;

namespace OrderPipeline.Functions.Adapters;

public sealed class DynamoDbOrderStore(
    IAmazonDynamoDB client,
    string tableName,
    ILogger<DynamoDbOrderStore> logger) : IOrderStore
{
    private const string DeadlinePassed = "The invocation ran out of time before DynamoDB answered.";

    public async Task<Result<Order>> PlaceAsync(Order order, CancellationToken cancellationToken)
    {
        var request = new PutItemRequest
        {
            TableName = tableName,
            ConditionExpression = $"attribute_not_exists({OrderAttributes.OrderId})",
            Item = new Dictionary<string, AttributeValue>
            {
                [OrderAttributes.OrderId] = new() { S = order.OrderId },
                [OrderAttributes.CustomerName] = new() { S = order.CustomerName },
                [OrderAttributes.Product] = new() { S = order.Product },
                [OrderAttributes.Amount] = new() { N = order.Amount.ToString(CultureInfo.InvariantCulture) },
                [OrderAttributes.Notes] = new() { S = order.Notes },
                [OrderAttributes.Status] = new() { S = order.Status.ToWireFormat() },
                [OrderAttributes.ValueTier] = new() { S = order.IsHighValue ? ValueTiers.HighValue : ValueTiers.Standard },
                [OrderAttributes.PlacedAt] = new() { S = order.PlacedAt.ToString("O", CultureInfo.InvariantCulture) },
                [OrderAttributes.RecencyBucket] = new() { S = RecencyBuckets.All },
                [OrderAttributes.RecencyCursor] = new() { S = RecencyCursor(order) }
            }
        };

        try
        {
            await client.PutItemAsync(request, cancellationToken);
        }
        catch (ConditionalCheckFailedException)
        {
            return Result<Order>.Failure($"Order '{order.OrderId}' already exists.");
        }
        catch (OperationCanceledException)
        {
            return Result<Order>.Failure(DeadlinePassed);
        }
        catch (AmazonDynamoDBException exception)
        {
            return Result<Order>.Failure(exception.Message);
        }

        return Result<Order>.Success(order);
    }

    public async Task<Result<Order>> GetAsync(string orderId, CancellationToken cancellationToken)
    {
        var request = new GetItemRequest
        {
            TableName = tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [OrderAttributes.OrderId] = new() { S = orderId }
            },
            // The worker reads an order moments after the dispatcher wrote it, which is exactly
            // the window an eventually consistent read is allowed to miss.
            ConsistentRead = true
        };

        try
        {
            var response = await client.GetItemAsync(request, cancellationToken);

            return response.IsItemSet && ToOrder(response.Item) is { } order
                ? Result<Order>.Success(order)
                : Result<Order>.Failure($"Order '{orderId}' was not found.");
        }
        catch (OperationCanceledException)
        {
            return Result<Order>.Failure(DeadlinePassed);
        }
        catch (AmazonDynamoDBException exception)
        {
            return Result<Order>.Failure(exception.Message);
        }
    }

    public async Task<Result<OrderPage>> ListAsync(OrderPageRequest page, CancellationToken cancellationToken)
    {
        var cursor = RecencyCursorCodec.Decode(page.ResumeFrom);
        if (!cursor.IsSuccess)
        {
            return Result<OrderPage>.Failure(cursor.Error!);
        }

        var request = new QueryRequest
        {
            TableName = tableName,
            IndexName = OrderIndexes.ByRecency,
            KeyConditionExpression = "#bucket = :bucket",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#bucket"] = OrderAttributes.RecencyBucket
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":bucket"] = new() { S = RecencyBuckets.All }
            },
            ScanIndexForward = false,
            Limit = page.Size,
            ExclusiveStartKey = cursor.Value
        };

        var orders = new List<Order>();

        try
        {
            var answer = await client.QueryAsync(request, cancellationToken);
            AddReadable(orders, answer.Items);

            return Result<OrderPage>.Success(
                new OrderPage(orders, RecencyCursorCodec.Encode(answer.LastEvaluatedKey)));
        }
        catch (OperationCanceledException)
        {
            return Result<OrderPage>.Failure(DeadlinePassed);
        }
        catch (AmazonDynamoDBException exception)
        {
            return Result<OrderPage>.Failure(exception.Message);
        }
    }

    public Task<Result<OrderTransition>> TransitionAsync(
        string orderId,
        OrderStatus from,
        OrderStatus to,
        CancellationToken cancellationToken) =>
        OrderLifecycle.CanTransition(from, to)
            ? ApplyAsync(MoveRequest(orderId, from, to), cancellationToken)
            : Task.FromResult(Result<OrderTransition>.Failure($"An order cannot move from {from} to {to}."));

    public Task<Result<OrderTransition>> FailAsync(
        string orderId,
        OrderStatus from,
        OrderFailure failure,
        CancellationToken cancellationToken)
    {
        if (!OrderLifecycle.CanTransition(from, OrderStatus.Failed))
        {
            return Task.FromResult(
                Result<OrderTransition>.Failure($"An order cannot move from {from} to {OrderStatus.Failed}."));
        }

        var request = MoveRequest(orderId, from, OrderStatus.Failed);
        request.UpdateExpression += ", #reason = :reason, #kind = :kind";
        request.ExpressionAttributeNames["#reason"] = OrderAttributes.FailureReason;
        request.ExpressionAttributeNames["#kind"] = OrderAttributes.FailureKind;
        request.ExpressionAttributeValues[":reason"] = new AttributeValue { S = failure.Reason };
        request.ExpressionAttributeValues[":kind"] = new AttributeValue { S = failure.Kind.ToWireFormat() };

        return ApplyAsync(request, cancellationToken);
    }

    // Not placedAt: that one is written with an offset suffix, and lexical order only matches
    // chronological order while every row carries the same offset. The order id breaks ties so
    // that two orders placed in the same tick still have a stable place in the sequence, which is
    // what makes a cursor safe to resume from.
    private static string RecencyCursor(Order order) =>
        order.PlacedAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture)
        + "#" + order.OrderId;

    private UpdateItemRequest MoveRequest(string orderId, OrderStatus from, OrderStatus to) => new()
    {
        TableName = tableName,
        Key = new Dictionary<string, AttributeValue>
        {
            [OrderAttributes.OrderId] = new() { S = orderId }
        },
        UpdateExpression = "SET #status = :to",
        ConditionExpression = "attribute_exists(#id) AND #status = :from",
        ExpressionAttributeNames = new Dictionary<string, string>
        {
            ["#id"] = OrderAttributes.OrderId,
            ["#status"] = OrderAttributes.Status
        },
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            [":from"] = new() { S = from.ToWireFormat() },
            [":to"] = new() { S = to.ToWireFormat() }
        }
    };

    private async Task<Result<OrderTransition>> ApplyAsync(
        UpdateItemRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.UpdateItemAsync(request, cancellationToken);
        }
        catch (ConditionalCheckFailedException)
        {
            return Result<OrderTransition>.Success(OrderTransition.AlreadyMovedOn);
        }
        catch (OperationCanceledException)
        {
            return Result<OrderTransition>.Failure(DeadlinePassed);
        }
        catch (AmazonDynamoDBException exception)
        {
            return Result<OrderTransition>.Failure(exception.Message);
        }

        return Result<OrderTransition>.Success(OrderTransition.Moved);
    }

    // A row the reader cannot make sense of is left out rather than failing the whole list, but
    // leaving it out silently would make an order disappear from the API with nothing to trace.
    private void AddReadable(List<Order> orders, IEnumerable<Dictionary<string, AttributeValue>> items)
    {
        foreach (var item in items)
        {
            if (ToOrder(item) is { } order)
            {
                orders.Add(order);
                continue;
            }

            logger.LogWarning(
                "Skipped a row that is not a readable order. {OrderId}",
                ReadTextOrEmpty(item, OrderAttributes.OrderId));
        }
    }

    private static Order? ToOrder(IDictionary<string, AttributeValue> item)
    {
        if (!TryReadText(item, OrderAttributes.OrderId, out var orderId)
            || !TryReadText(item, OrderAttributes.Status, out var status)
            || !Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsedStatus)
            || !TryReadNumber(item, OrderAttributes.Amount, out var amount)
            || !TryReadTimestamp(item, OrderAttributes.PlacedAt, out var placedAt))
        {
            return null;
        }

        return new Order
        {
            OrderId = orderId,
            CustomerName = ReadTextOrEmpty(item, OrderAttributes.CustomerName),
            Product = ReadTextOrEmpty(item, OrderAttributes.Product),
            Amount = amount,
            Notes = ReadTextOrEmpty(item, OrderAttributes.Notes),
            Status = parsedStatus,
            IsHighValue = ReadTextOrEmpty(item, OrderAttributes.ValueTier) == ValueTiers.HighValue,
            PlacedAt = placedAt
        };
    }

    private static bool TryReadText(IDictionary<string, AttributeValue> item, string name, out string value)
    {
        value = ReadTextOrEmpty(item, name);
        return value.Length > 0;
    }

    private static string ReadTextOrEmpty(IDictionary<string, AttributeValue> item, string name) =>
        item.TryGetValue(name, out var attribute) ? attribute.S ?? string.Empty : string.Empty;

    private static bool TryReadNumber(IDictionary<string, AttributeValue> item, string name, out decimal value) =>
        decimal.TryParse(
            item.TryGetValue(name, out var attribute) ? attribute.N : null,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out value);

    private static bool TryReadTimestamp(IDictionary<string, AttributeValue> item, string name, out DateTimeOffset value) =>
        DateTimeOffset.TryParse(
            ReadTextOrEmpty(item, name),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out value);
}
