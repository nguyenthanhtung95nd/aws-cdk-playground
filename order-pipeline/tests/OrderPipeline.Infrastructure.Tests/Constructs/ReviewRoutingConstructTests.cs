using Amazon.CDK.Assertions;

namespace OrderPipeline.Infrastructure.Tests.Constructs;

public class ReviewRoutingConstructTests
{
    [Fact]
    public void Synthesize_Always_CopiesOnlyHighValuePlacementsIntoTheReviewLane()
    {
        var pipe = Pipe(StackTemplate.SynthesizeWith());

        Assert.Contains("HIGH_VALUE", pipe);
        Assert.Contains("valueTier", pipe);
        Assert.Contains("INSERT", pipe);
    }

    [Fact]
    public void Synthesize_Always_FeedsTheRoutingRuleFromTheChangeStreamAndNotTheWorkQueue()
    {
        var pipe = Pipe(StackTemplate.SynthesizeWith());

        Assert.Contains("DynamoDBStreamParameters", pipe);
        Assert.Contains("StreamArn", pipe);
        Assert.DoesNotContain("SqsQueueParameters", pipe);
    }

    // Left alone the routing rule replays a record it cannot deliver until the record ages out of
    // the stream a day later, and then loses it with nothing recorded anywhere.
    [Fact]
    public void Synthesize_Always_StopsTheRoutingRuleReplayingForeverAndKeepsWhatItGivesUpOn()
    {
        var template = StackTemplate.SynthesizeWith();
        var pipe = Pipe(template);

        Assert.Contains("\"MaximumRetryAttempts\":3", pipe);
        Assert.Contains("DeadLetterConfig", pipe);
        Assert.Contains(StreamParkingArea(template), pipe);
    }

    [Fact]
    public void Synthesize_Always_LetsTheRoutingRuleSayWhyItFailed()
    {
        var pipe = Pipe(StackTemplate.SynthesizeWith());

        Assert.Contains("LogConfiguration", pipe);
        Assert.Contains("CloudwatchLogsLogDestination", pipe);
        Assert.DoesNotContain("\"Level\":\"OFF\"", pipe);
    }

    [Fact]
    public void Synthesize_Always_SendsTheRoutingRuleOnlyTheOrderId()
    {
        var pipe = Pipe(StackTemplate.SynthesizeWith());

        Assert.Contains("dynamodb.NewImage.orderId.S", pipe);
    }

    [Fact]
    public void Synthesize_Always_LetsTheRoutingRuleReadTheStreamAndWriteOnlyToTheReviewQueue()
    {
        var template = StackTemplate.SynthesizeWith();

        var routing = System.Text.Json.JsonSerializer.Serialize(
            template.FindResources("AWS::IAM::Policy")
                .Single(policy => policy.Key.Contains("reviewrouting", StringComparison.OrdinalIgnoreCase))
                .Value);

        Assert.Contains("dynamodb:GetShardIterator", routing);
        Assert.Contains("sqs:SendMessage", routing);
        Assert.DoesNotContain("dynamodb:PutItem", routing);
        Assert.DoesNotContain("dynamodb:UpdateItem", routing);
        Assert.DoesNotContain("sqs:ReceiveMessage", routing);
    }

    private static string Pipe(Template template) =>
        System.Text.Json.JsonSerializer.Serialize(template.FindResources("AWS::Pipes::Pipe").Values.Single());

    // Both references point at a logical id, and CDK strips the hyphens out of the queue name to
    // build it, so the readable name never appears in either resource.
    private static string StreamParkingArea(Template template) =>
        template.FindResources("AWS::SQS::Queue")
            .Single(queue => System.Text.Json.JsonSerializer.Serialize(queue.Value)
                .Contains("orderpipeline-dev-demo-stream-parking"))
            .Key;
}
