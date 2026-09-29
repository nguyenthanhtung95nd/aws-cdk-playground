using System.Text.Json;
using Amazon.CDK.Assertions;

namespace OrderPipeline.Infrastructure.Tests.Constructs;

public class MonitoringConstructTests
{
    // Naming the alarms this test expects is what lets a missing one show up here. An earlier
    // version counted the three that happened to exist, which turned the review queue having no
    // alarm at all into the documented answer.
    private static readonly string[] AlarmNames =
    [
        "parking-area-occupied",
        "work-queue-stale",
        "function-errors",
        "stream-parking-filled",
        "review-queue-stale",
        "review-routing-broken",
        "stream-falling-behind"
    ];

    public static TheoryData<string> EveryAlarm => [.. AlarmNames];

    [Theory]
    [MemberData(nameof(EveryAlarm))]
    public void Synthesize_Always_WatchesEveryLaneAnOrderCanBeLostIn(string name)
    {
        StackTemplate.SynthesizeWith().HasResourceProperties("AWS::CloudWatch::Alarm", Match.ObjectLike(
            new Dictionary<string, object> { ["AlarmName"] = $"orderpipeline-dev-demo-{name}" }));
    }

    [Fact]
    public void Synthesize_Always_RaisesNoAlarmNobodyNamed()
    {
        StackTemplate.SynthesizeWith().ResourceCountIs("AWS::CloudWatch::Alarm", AlarmNames.Length);
    }

    [Fact]
    public void Synthesize_Always_WatchesTheReviewQueueTheSuppressionPromisedToWatch()
    {
        var alarm = Serialized(StackTemplate.SynthesizeWith(), "review-queue-stale");

        Assert.Contains("ApproximateAgeOfOldestMessage", alarm);
        Assert.Contains("orderpipeline-dev-demo-review", alarm);
    }

    // Nothing drains the stream parking area, so an alarm on how many messages sit in it would
    // never fall silent again once one arrived.
    [Fact]
    public void Synthesize_Always_WatchesArrivalsInTheStreamParkingAreaRatherThanItsDepth()
    {
        var alarm = Serialized(StackTemplate.SynthesizeWith(), "stream-parking-filled");

        Assert.Contains("NumberOfMessagesSent", alarm);
        Assert.DoesNotContain("ApproximateNumberOfMessagesVisible", alarm);
    }

    [Fact]
    public void Synthesize_Always_WatchesTheRoutingRuleBecauseAnEmptyReviewQueueLooksCalm()
    {
        var alarm = Serialized(StackTemplate.SynthesizeWith(), "review-routing-broken");

        Assert.Contains("AWS/EventBridge/Pipes", alarm);
        Assert.Contains("ExecutionFailed", alarm);
        Assert.Contains("TargetStageFailed", alarm);
        Assert.Contains("ExecutionThrottled", alarm);
        Assert.Contains("orderpipeline-dev-demo-review-routing", alarm);
    }

    // Lambda reports this one in milliseconds while the queue ages above are in seconds, so a
    // threshold that looks right next to them would fire three hundred times too early.
    [Fact]
    public void Synthesize_Always_MeasuresStreamLagInMillisecondsNotSeconds()
    {
        var alarm = Serialized(StackTemplate.SynthesizeWith(), "stream-falling-behind");

        Assert.Contains("IteratorAge", alarm);
        Assert.Contains("Threshold\":300000", alarm);
    }

    [Fact]
    public void Synthesize_Always_TreatsSilenceAsHealthSoAnIdleSystemNeverCries()
    {
        var template = StackTemplate.SynthesizeWith();

        template.AllResourcesProperties("AWS::CloudWatch::Alarm", Match.ObjectLike(
            new Dictionary<string, object> { ["TreatMissingData"] = "notBreaching" }));
    }

    [Fact]
    public void Synthesize_Always_SendsEveryAlarmToTheSameTopic()
    {
        var template = StackTemplate.SynthesizeWith();

        template.ResourceCountIs("AWS::SNS::Topic", 1);
        template.AllResourcesProperties("AWS::CloudWatch::Alarm", Match.ObjectLike(
            new Dictionary<string, object> { ["AlarmActions"] = Match.AnyValue() }));
    }

    [Fact]
    public void Synthesize_Always_CountsErrorsFromEveryFunctionNotJustOne()
    {
        var template = StackTemplate.SynthesizeWith();

        var functions = StackTemplate.OurFunctions(template).Count;
        var errorAlarm = Serialized(template, "function-errors");

        Assert.Equal(functions, Occurrences(errorAlarm, "MetricName\":\"Errors"));
    }

    [Fact]
    public void Synthesize_Always_CriesAsSoonAsAnythingIsParked()
    {
        var alarm = Serialized(StackTemplate.SynthesizeWith(), "parking-area-occupied");

        Assert.Contains("ApproximateNumberOfMessagesVisible", alarm);
        Assert.Contains("Threshold\":0", alarm);
        Assert.Contains("GreaterThanThreshold", alarm);
    }

    [Fact]
    public void Synthesize_Always_CriesWhenTheOldestMessageHasWaitedFiveMinutes()
    {
        var alarm = Serialized(StackTemplate.SynthesizeWith(), "work-queue-stale");

        Assert.Contains("ApproximateAgeOfOldestMessage", alarm);
        Assert.Contains("Threshold\":300", alarm);
    }

    [Fact]
    public void Synthesize_WithoutAnAlertAddress_SubscribesNobodyRatherThanGuessing()
    {
        StackTemplate.SynthesizeWith().ResourceCountIs("AWS::SNS::Subscription", 0);
    }

    [Fact]
    public void Synthesize_WithAnAlertAddress_SendsThereByEmail()
    {
        var template = StackTemplate.SynthesizeWith(alertEmail: "oncall@example.com");

        template.HasResourceProperties("AWS::SNS::Subscription", Match.ObjectLike(
            new Dictionary<string, object>
            {
                ["Protocol"] = "email",
                ["Endpoint"] = "oncall@example.com"
            }));
    }

    [Fact]
    public void Synthesize_Always_ShowsEveryFunctionAndEveryQueueOnOneDashboard()
    {
        var template = StackTemplate.SynthesizeWith();

        template.ResourceCountIs("AWS::CloudWatch::Dashboard", 1);

        var dashboard = JsonSerializer.Serialize(
            template.FindResources("AWS::CloudWatch::Dashboard").Values.Single());
        var functions = StackTemplate.OurFunctions(template).Count;

        Assert.Equal(functions, Occurrences(dashboard, "Invocations"));
        Assert.Equal(functions, Occurrences(dashboard, "Duration"));
        var queues = template.FindResources("AWS::SQS::Queue").Count;

        Assert.Equal(queues, Occurrences(dashboard, "ApproximateNumberOfMessagesVisible"));
        Assert.Equal(queues, Occurrences(dashboard, "ApproximateAgeOfOldestMessage"));
    }

    private static string Serialized(Template template, string alarmName) =>
        template.FindResources("AWS::CloudWatch::Alarm")
            .Values
            .Select(alarm => JsonSerializer.Serialize(alarm))
            .Single(alarm => alarm.Contains(alarmName));

    private static int Occurrences(string text, string value) =>
        text.Split(value).Length - 1;
}
