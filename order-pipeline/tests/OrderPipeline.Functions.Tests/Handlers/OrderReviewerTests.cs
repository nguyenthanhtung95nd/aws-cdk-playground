using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using NSubstitute;
using OrderPipeline.Contracts;
using OrderPipeline.Functions.Handlers;
using OrderPipeline.Functions.Tests.Fakes;

namespace OrderPipeline.Functions.Tests.Handlers;

public class OrderReviewerTests
{
    private readonly RecordingLogger<OrderReviewer> _logger = new();
    private readonly OrderReviewer _sut;

    public OrderReviewerTests() => _sut = new OrderReviewer(_logger);

    [Fact]
    public void ReviewOrders_ForAHighValueOrder_RecordsThatItNeedsReview()
    {
        _sut.ReviewOrders(EventOf(ToReview("order-1")), Context());

        Assert.Contains(_logger.Lines, line =>
            line.Level == LogLevel.Information && line.Message.Contains("order-1"));
    }

    [Fact]
    public void ReviewOrders_ForEveryOrderInABatch_LeavesNothingUnreviewed()
    {
        var response = _sut.ReviewOrders(
            EventOf(ToReview("order-1"), ToReview("order-2"), ToReview("order-3")), Context());

        Assert.Empty(response.BatchItemFailures);
        Assert.Equal(3, _logger.Lines.Count(line => line.Level == LogLevel.Information));
    }

    [Fact]
    public void ReviewOrders_ForAMessageWithoutAnOrderId_AsksForItBack()
    {
        var response = _sut.ReviewOrders(EventOf(ToReview(string.Empty)), Context());

        Assert.Single(response.BatchItemFailures);
    }

    [Fact]
    public void ReviewOrders_Always_TagsEveryLogLineWithTheOrderUnderReview()
    {
        _sut.ReviewOrders(EventOf(ToReview("order-1")), Context());

        Assert.NotEmpty(_logger.Lines);
        Assert.All(_logger.Lines, line =>
        {
            Assert.Equal("order-1", line.Scope[LogFields.CorrelationId]);
            Assert.Equal("req-reviewer", line.Scope[LogFields.InvocationId]);
        });
    }

    private static SQSEvent.SQSMessage ToReview(string orderId) =>
        new() { MessageId = $"review-{orderId}", Body = orderId };

    private static SQSEvent EventOf(params SQSEvent.SQSMessage[] messages) =>
        new() { Records = [.. messages] };

    private static ILambdaContext Context()
    {
        var context = Substitute.For<ILambdaContext>();
        context.AwsRequestId.Returns("req-reviewer");
        return context;
    }
}
