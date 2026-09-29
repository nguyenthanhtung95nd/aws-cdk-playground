using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Constructs;

namespace OrderPipeline.Infrastructure.Constructs;

public sealed class MonitoringConstructProps
{
    public required StageConfig Stage { get; init; }

    public required IReadOnlyList<Function> Functions { get; init; }

    public required Queue WorkQueue { get; init; }

    public required Queue WorkParkingArea { get; init; }

    public required Queue ReviewQueue { get; init; }

    public required Queue StreamParkingArea { get; init; }

    public required Function StreamDispatcher { get; init; }

    public required string ReviewRoutingPipeName { get; init; }
}

public sealed class MonitoringConstruct : Construct
{
    private static readonly Duration AlarmWindow = Duration.Minutes(1);

    private static readonly Duration TolerableAge = Duration.Minutes(5);

    // Lambda reports iterator age in milliseconds, unlike the queue ages above, which are seconds.
    private static readonly Duration TolerableStreamLag = Duration.Minutes(5);

    // There is no L2 construct for a pipe in aws-cdk-lib, so nothing hands these metrics over
    // ready-made, and the namespace is not the one the CloudWatch namespace index would suggest.
    private const string PipesNamespace = "AWS/EventBridge/Pipes";

    private readonly StageConfig _stage;

    public MonitoringConstruct(Construct scope, string id, MonitoringConstructProps props) : base(scope, id)
    {
        _stage = props.Stage;

        var topic = new Topic(this, "alerts", new TopicProps
        {
            TopicName = $"{props.Stage.StackId}-alerts",
            DisplayName = "Order pipeline alerts",
            EnforceSSL = true
        });

        if (props.Stage.AlertEmail.Length > 0)
        {
            topic.AddSubscription(new EmailSubscription(props.Stage.AlertEmail));
        }

        Alarm(topic, "parking-area-occupied", props.WorkParkingArea.MetricApproximateNumberOfMessagesVisible(
            new MetricOptions { Period = AlarmWindow, Statistic = "Maximum" }), 0);

        Alarm(topic, "work-queue-stale", props.WorkQueue.MetricApproximateAgeOfOldestMessage(
            new MetricOptions { Period = AlarmWindow, Statistic = "Maximum" }), TolerableAge.ToSeconds());

        Alarm(topic, "function-errors", TotalErrors(props.Functions), 0);

        // Nothing drains the stream parking area, so watching how many messages sit in it would
        // leave this alarm ringing from the first failure until somebody emptied the queue by hand.
        // Watching arrivals says the same thing and falls quiet on its own.
        Alarm(topic, "stream-parking-filled", props.StreamParkingArea.MetricNumberOfMessagesSent(
            new MetricOptions { Period = AlarmWindow, Statistic = "Sum" }), 0);

        Alarm(topic, "review-queue-stale", props.ReviewQueue.MetricApproximateAgeOfOldestMessage(
            new MetricOptions { Period = AlarmWindow, Statistic = "Maximum" }), TolerableAge.ToSeconds());

        // A broken routing rule leaves the review queue empty, which the alarm above reads as calm.
        Alarm(topic, "review-routing-broken", RoutingTrouble(props.ReviewRoutingPipeName), 0);

        Alarm(topic, "stream-falling-behind", props.StreamDispatcher.Metric(
            "IteratorAge",
            new MetricOptions { Period = AlarmWindow, Statistic = "Maximum", Unit = Unit.MILLISECONDS }),
            TolerableStreamLag.ToMilliseconds());

        _ = new Dashboard(this, "dashboard", new DashboardProps
        {
            DashboardName = $"{props.Stage.StackId}",
            Widgets = new[]
            {
                new IWidget[] { QueueDepths(props), QueueAges(props) },
                new IWidget[] { FunctionInvocations(props.Functions), FunctionErrors(props.Functions) },
                new IWidget[] { FunctionDurations(props.Functions) }
            }
        });
    }

    // Every alarm here watches something that is normally absent rather than normally zero, and a
    // queue with nothing in it reports no data at all. Treating that silence as a breach would make
    // all three alarms cry on an idle system, and an alarm that cries wrongly once is ignored after.
    private void Alarm(ITopic topic, string id, IMetric metric, double threshold)
    {
        var alarm = new Alarm(this, id, new AlarmProps
        {
            AlarmName = $"{_stage.StackId}-{id}",
            Metric = metric,
            Threshold = threshold,
            EvaluationPeriods = 1,
            ComparisonOperator = ComparisonOperator.GREATER_THAN_THRESHOLD,
            TreatMissingData = TreatMissingData.NOT_BREACHING
        });

        alarm.AddAlarmAction(new SnsAction(topic));
    }

    private static IMetric TotalErrors(IReadOnlyList<Function> functions) => new MathExpression(
        new MathExpressionProps
        {
            Label = "Errors across every function",
            Expression = string.Join(" + ", functions.Select((_, index) => $"e{index}")),
            UsingMetrics = functions
                .Select((function, index) => (Key: $"e{index}", Metric: function.MetricErrors(
                    new MetricOptions { Period = AlarmWindow, Statistic = "Sum" })))
                .ToDictionary(entry => entry.Key, entry => (IMetric)entry.Metric),
            Period = AlarmWindow
        });

    private static IMetric RoutingTrouble(string pipeName) => new MathExpression(new MathExpressionProps
    {
        Label = "Anything the review routing could not deliver",
        Expression = "failed + targetFailed + throttled",
        UsingMetrics = new Dictionary<string, IMetric>
        {
            ["failed"] = PipeMetric(pipeName, "ExecutionFailed"),
            ["targetFailed"] = PipeMetric(pipeName, "TargetStageFailed"),
            ["throttled"] = PipeMetric(pipeName, "ExecutionThrottled")
        },
        Period = AlarmWindow
    });

    private static Metric PipeMetric(string pipeName, string metricName) => new(new MetricProps
    {
        Namespace = PipesNamespace,
        MetricName = metricName,
        DimensionsMap = new Dictionary<string, string> { ["PipeName"] = pipeName },
        Period = AlarmWindow,
        Statistic = "Sum"
    });

    private static GraphWidget QueueDepths(MonitoringConstructProps props) => new(new GraphWidgetProps
    {
        Title = "Messages waiting",
        Left = new IMetric[]
        {
            props.WorkQueue.MetricApproximateNumberOfMessagesVisible(),
            props.WorkParkingArea.MetricApproximateNumberOfMessagesVisible(),
            props.ReviewQueue.MetricApproximateNumberOfMessagesVisible(),
            props.StreamParkingArea.MetricApproximateNumberOfMessagesVisible()
        }
    });

    private static GraphWidget QueueAges(MonitoringConstructProps props) => new(new GraphWidgetProps
    {
        Title = "Age of oldest message",
        Left = new IMetric[]
        {
            props.WorkQueue.MetricApproximateAgeOfOldestMessage(),
            props.WorkParkingArea.MetricApproximateAgeOfOldestMessage(),
            props.ReviewQueue.MetricApproximateAgeOfOldestMessage(),
            props.StreamParkingArea.MetricApproximateAgeOfOldestMessage()
        }
    });

    private static GraphWidget FunctionInvocations(IReadOnlyList<Function> functions) => new(new GraphWidgetProps
    {
        Title = "How often each function ran",
        Left = functions.Select(function => (IMetric)function.MetricInvocations()).ToArray()
    });

    private static GraphWidget FunctionErrors(IReadOnlyList<Function> functions) => new(new GraphWidgetProps
    {
        Title = "Failures and throttles",
        Left = functions.Select(function => (IMetric)function.MetricErrors())
            .Concat(functions.Select(function => (IMetric)function.MetricThrottles()))
            .ToArray()
    });

    private static GraphWidget FunctionDurations(IReadOnlyList<Function> functions) => new(new GraphWidgetProps
    {
        Title = "How long each function took",
        Left = functions.Select(function => (IMetric)function.MetricDuration()).ToArray()
    });
}
