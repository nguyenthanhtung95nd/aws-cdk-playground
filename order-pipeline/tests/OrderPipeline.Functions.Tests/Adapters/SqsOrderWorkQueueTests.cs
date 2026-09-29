using Amazon.SQS;
using Amazon.SQS.Model;
using NSubstitute;
using OrderPipeline.Functions.Adapters;

namespace OrderPipeline.Functions.Tests.Adapters;

public class SqsOrderWorkQueueTests
{
    private const string QueueUrl = "https://sqs.test/work";
    private const string OrderId = "order-1";

    private readonly IAmazonSQS _client = Substitute.For<IAmazonSQS>();

    public SqsOrderWorkQueueTests() =>
        _client.SendMessageAsync(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SendMessageResponse { MessageId = "message-1" });

    [Fact]
    public async Task EnqueueAsync_WhenTheQueueAccepts_ReportsTheMessageItCreated()
    {
        var result = await Enqueue();

        Assert.True(result.IsSuccess);
        Assert.Equal("message-1", result.Value);
    }

    [Fact]
    public async Task EnqueueAsync_Always_SendsTheOrderIdToTheConfiguredQueue()
    {
        var request = await CaptureSend();

        Assert.Equal(QueueUrl, request.QueueUrl);
        Assert.Equal(OrderId, request.MessageBody);
    }

    [Fact]
    public async Task EnqueueAsync_WhenNoTraceIsActive_SendsNoAttributes()
    {
        var request = await CaptureSend();

        Assert.Null(request.MessageAttributes);
    }

    [Fact]
    public async Task EnqueueAsync_WhenATraceIsActive_CarriesItsIdInTheMessage()
    {
        using var activity = new System.Diagnostics.Activity("dispatch").Start();

        var request = await CaptureSend();

        Assert.Equal(activity.Id, request.MessageAttributes[Contracts.MessageAttributes.TraceParent].StringValue);
    }

    [Fact]
    public async Task EnqueueAsync_WhenTheQueueRejects_ReportsTheReason()
    {
        _client.SendMessageAsync(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<SendMessageResponse>>(_ => throw new AmazonSQSException("the queue is gone"));

        var result = await Enqueue();

        Assert.False(result.IsSuccess);
        Assert.Contains("the queue is gone", result.Error);
    }

    [Fact]
    public async Task EnqueueAsync_WhenTheInvocationRunsOutOfTime_SaysSoInsteadOfThrowing()
    {
        _client.SendMessageAsync(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<SendMessageResponse>>(_ => throw new OperationCanceledException());

        var result = await Enqueue();

        Assert.False(result.IsSuccess);
        Assert.Contains("ran out of time", result.Error);
    }

    private Task<Domain.Common.Result<string>> Enqueue() =>
        new SqsOrderWorkQueue(_client, QueueUrl).EnqueueAsync(OrderId, CancellationToken.None);

    private async Task<SendMessageRequest> CaptureSend()
    {
        await Enqueue();

        return (SendMessageRequest)_client.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IAmazonSQS.SendMessageAsync))
            .GetArguments()[0]!;
    }
}
