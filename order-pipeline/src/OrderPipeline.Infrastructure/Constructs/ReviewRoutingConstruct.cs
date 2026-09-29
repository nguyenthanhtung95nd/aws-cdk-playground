using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.Pipes;
using Amazon.CDK.AWS.SQS;
using Cdklabs.CdkNag;
using Constructs;
using OrderPipeline.Contracts;

namespace OrderPipeline.Infrastructure.Constructs;

public sealed class ReviewRoutingConstructProps
{
    public required StageConfig Stage { get; init; }

    public required Table OrdersTable { get; init; }

    public required Queue ReviewQueue { get; init; }

    public required Queue StreamParkingArea { get; init; }
}

/// <summary>
/// Copies high-value placements out of the change stream into the review queue. Reading the work
/// queue instead would take orders away from the workers that still need them.
/// </summary>
public sealed class ReviewRoutingConstruct : Construct
{
    private static readonly string HighValueInsertPattern =
        "{\"eventName\":[\"INSERT\"],\"dynamodb\":{\"NewImage\":{\""
        + OrderAttributes.ValueTier + "\":{\"S\":[\"" + ValueTiers.HighValue + "\"]}}}}";

    public string PipeName { get; }

    public ReviewRoutingConstruct(Construct scope, string id, ReviewRoutingConstructProps props) : base(scope, id)
    {
        PipeName = $"{props.Stage.StackId}-review-routing";

        var role = CreateRole(props);
        var logGroup = CreateLogGroup(role);

        _ = new CfnPipe(this, "pipe", new CfnPipeProps
        {
            Name = PipeName,
            RoleArn = role.RoleArn,
            Source = props.OrdersTable.TableStreamArn!,
            LogConfiguration = LogConfiguration(logGroup),
            SourceParameters = SourceParameters(props.StreamParkingArea),
            Target = props.ReviewQueue.QueueArn,
            TargetParameters = new CfnPipe.PipeTargetParametersProperty
            {
                InputTemplate = $"<$.dynamodb.NewImage.{OrderAttributes.OrderId}.S>"
            }
        });
    }

    private Role CreateRole(ReviewRoutingConstructProps props)
    {
        var role = new Role(this, "role", new RoleProps
        {
            AssumedBy = new ServicePrincipal("pipes.amazonaws.com")
        });

        props.OrdersTable.GrantStreamRead(role);
        props.ReviewQueue.GrantSendMessages(role);
        props.StreamParkingArea.GrantSendMessages(role);

        NagSuppressions.AddResourceSuppressions(role, new[]
        {
            new NagPackSuppression
            {
                Id = "AwsSolutions-IAM5",
                Reason = "Only 'dynamodb:ListStreams' is wildcarded, and only because AWS does not "
                       + "accept a resource on that action.",
                AppliesTo = new[] { "Resource::*" }
            }
        }, applyToChildren: true);

        return role;
    }

    // A pipe left to itself writes no logs at all, so the one thing that decides whether a
    // high-value order is ever looked at would fail without saying anything.
    private LogGroup CreateLogGroup(Role role)
    {
        var logGroup = new LogGroup(this, "logs", new LogGroupProps
        {
            LogGroupName = $"/aws/vendedlogs/pipes/{PipeName}",
            Retention = RetentionDays.ONE_WEEK,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        logGroup.GrantWrite(role);

        return logGroup;
    }

    private static CfnPipe.PipeLogConfigurationProperty LogConfiguration(LogGroup logGroup) => new()
    {
        Level = "ERROR",
        IncludeExecutionData = new[] { "ALL" },
        CloudwatchLogsLogDestination = new CfnPipe.CloudwatchLogsLogDestinationProperty
        {
            LogGroupArn = logGroup.LogGroupArn
        }
    };

    private static CfnPipe.PipeSourceParametersProperty SourceParameters(Queue parkingArea) => new()
    {
        DynamoDbStreamParameters = new CfnPipe.PipeSourceDynamoDBStreamParametersProperty
        {
            StartingPosition = "LATEST",
            BatchSize = 1,
            // Both default to -1, forever: a record the review queue keeps refusing would be
            // replayed until it aged out of the stream a day later.
            MaximumRetryAttempts = FunctionDefaults.PipeRetryBudget,
            DeadLetterConfig = new CfnPipe.DeadLetterConfigProperty
            {
                Arn = parkingArea.QueueArn
            }
        },
        FilterCriteria = new CfnPipe.FilterCriteriaProperty
        {
            Filters = new[]
            {
                new CfnPipe.FilterProperty { Pattern = HighValueInsertPattern }
            }
        }
    };
}
