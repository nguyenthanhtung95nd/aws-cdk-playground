using System.Diagnostics;
using Amazon.SQS;
using Amazon.SQS.Model;
using OrderPipeline.Contracts;
using OrderPipeline.Domain.Common;
using OrderPipeline.Domain.Ports;

namespace OrderPipeline.Functions.Adapters;

public sealed class SqsOrderWorkQueue(IAmazonSQS client, string queueUrl) : IOrderWorkQueue
{
    private const string DeadlinePassed = "The invocation ran out of time before the queue answered.";

    public async Task<Result<string>> EnqueueAsync(string orderId, CancellationToken cancellationToken)
    {
        var request = new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = orderId
        };

        // The consumer runs in another process; the trace id is the only thing that lets its
        // span join this one.
        if (Activity.Current?.Id is { } traceParent)
        {
            request.MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [MessageAttributes.TraceParent] = new() { DataType = "String", StringValue = traceParent }
            };
        }

        try
        {
            var sent = await client.SendMessageAsync(request, cancellationToken);
            return Result<string>.Success(sent.MessageId);
        }
        catch (OperationCanceledException)
        {
            return Result<string>.Failure(DeadlinePassed);
        }
        catch (AmazonSQSException exception)
        {
            return Result<string>.Failure(exception.Message);
        }
    }
}
