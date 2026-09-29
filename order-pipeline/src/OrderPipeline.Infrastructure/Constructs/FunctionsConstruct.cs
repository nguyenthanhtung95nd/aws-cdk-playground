using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.SQS;
using Cdklabs.CdkNag;
using Constructs;
using OrderPipeline.Contracts;

namespace OrderPipeline.Infrastructure.Constructs;

public sealed class FunctionsConstructProps
{
    public required StageConfig Stage { get; init; }

    public required Table OrdersTable { get; init; }

    public required Queue WorkQueue { get; init; }

    public required Queue WorkParkingArea { get; init; }

    public required Queue ReviewQueue { get; init; }

    public required Queue StreamParkingArea { get; init; }
}

/// <summary>
/// The seven Lambda functions, what each may touch, and what feeds each of them.
/// </summary>
public sealed class FunctionsConstruct : Construct
{
    private const int BatchSize = 10;

    public Function GetHealth { get; }

    public Function PlaceOrder { get; }

    public Function ListOrders { get; }

    public Function Dispatcher { get; }

    public Function Worker { get; }

    public Function Settler { get; }

    public Function Reviewer { get; }

    public IReadOnlyList<Function> All { get; }

    public FunctionsConstruct(Construct scope, string id, FunctionsConstructProps props) : base(scope, id)
    {
        var factory = new LambdaFunctionFactory(this, props.Stage);

        var dispatcherConfiguration = OrderConfiguration(props);
        dispatcherConfiguration[EnvironmentVariables.WorkQueueUrl] = props.WorkQueue.QueueUrl;

        GetHealth = factory.Create("get-health", LambdaHandlers.GetHealth, new Dictionary<string, string>());
        PlaceOrder = factory.Create("place-order", LambdaHandlers.PlaceOrder, OrderConfiguration(props));
        ListOrders = factory.Create("list-orders", LambdaHandlers.ListOrders, OrderConfiguration(props));
        Dispatcher = factory.Create("dispatch-placed-orders", LambdaHandlers.DispatchPlacedOrders, dispatcherConfiguration);
        Worker = factory.Create("complete-orders", LambdaHandlers.CompleteOrders, OrderConfiguration(props));
        Settler = factory.Create("settle-failures", LambdaHandlers.SettleFailures, OrderConfiguration(props));
        Reviewer = factory.Create("review-orders", LambdaHandlers.ReviewOrders, new Dictionary<string, string>());

        All = [GetHealth, PlaceOrder, ListOrders, Dispatcher, Worker, Settler, Reviewer];

        GrantTableAccess(props.OrdersTable);
        GrantQueueAccess(props);
        FeedFromQueues(props);
        FeedFromStream(props);
    }

    // Every grant names the exact actions and the exact resource. Table.Grant would widen each
    // of them to the indexes as well, and the emulator does not enforce policy, so that would
    // only show up once deployed.
    private void GrantTableAccess(ITable table)
    {
        AllowOnTable(PlaceOrder, table, "dynamodb:PutItem");
        AllowOnTable(Dispatcher, table, "dynamodb:UpdateItem");
        AllowOnTable(Worker, table, "dynamodb:GetItem", "dynamodb:UpdateItem");
        AllowOnTable(Settler, table, "dynamodb:UpdateItem");
        AllowOnIndex(ListOrders, table, OrderIndexes.ByRecency, "dynamodb:Query");
    }

    private void GrantQueueAccess(FunctionsConstructProps props)
    {
        props.WorkQueue.GrantSendMessages(Dispatcher);
        props.WorkQueue.GrantConsumeMessages(Worker);
        props.WorkParkingArea.GrantConsumeMessages(Settler);
        props.ReviewQueue.GrantConsumeMessages(Reviewer);
    }

    private void FeedFromQueues(FunctionsConstructProps props)
    {
        Worker.AddEventSource(QueueSource(props.WorkQueue));
        Settler.AddEventSource(QueueSource(props.WorkParkingArea));
        Reviewer.AddEventSource(QueueSource(props.ReviewQueue));
    }

    private void FeedFromStream(FunctionsConstructProps props)
    {
        Dispatcher.AddEventSource(new DynamoEventSource(props.OrdersTable, new DynamoEventSourceProps
        {
            StartingPosition = StartingPosition.TRIM_HORIZON,
            BatchSize = BatchSize,
            // Otherwise one bad record sends the whole batch round again.
            ReportBatchItemFailures = true,
            RetryAttempts = FunctionDefaults.StreamRetryBudget,
            // Otherwise a spent record is dropped and its order stays pending for good.
            OnFailure = new SqsDlq(props.StreamParkingArea)
        }));

        NagSuppressions.AddResourceSuppressions(Dispatcher.Role!, new[]
        {
            new NagPackSuppression
            {
                Id = "AwsSolutions-IAM5",
                Reason = "Only 'dynamodb:ListStreams' is wildcarded, and only because AWS does not "
                       + "accept a resource on that action. Every other permission this function "
                       + "holds names the single table it reads from and writes to.",
                AppliesTo = new[] { "Resource::*" }
            }
        }, applyToChildren: true);
    }

    private static SqsEventSource QueueSource(IQueue queue) => new(queue, new SqsEventSourceProps
    {
        BatchSize = BatchSize,
        ReportBatchItemFailures = true
    });

    private static void AllowOnTable(Function function, ITable table, params string[] actions) =>
        Allow(function, actions, table.TableArn);

    private static void AllowOnIndex(Function function, ITable table, string index, params string[] actions) =>
        Allow(function, actions, $"{table.TableArn}/index/{index}");

    private static void Allow(Function function, string[] actions, string resource) =>
        function.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = actions,
            Resources = new[] { resource }
        }));

    private static Dictionary<string, string> OrderConfiguration(FunctionsConstructProps props) => new()
    {
        [EnvironmentVariables.OrdersTableName] = props.OrdersTable.TableName,
        [EnvironmentVariables.HighValueThreshold] = props.Stage.HighValueThreshold,
        [EnvironmentVariables.BusinessLimit] = props.Stage.BusinessLimit
    };
}
