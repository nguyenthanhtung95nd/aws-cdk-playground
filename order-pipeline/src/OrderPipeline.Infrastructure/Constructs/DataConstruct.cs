using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.SQS;
using Cdklabs.CdkNag;
using OrderPipeline.Contracts;
using Constructs;

namespace OrderPipeline.Infrastructure.Constructs;

public sealed class DataConstructProps
{
    public required StageConfig Stage { get; init; }
}

public sealed class DataConstruct : Construct
{
    public Table OrdersTable { get; }

    public Queue WorkQueue { get; }

    public Queue WorkParkingArea { get; }

    public Queue ReviewQueue { get; }

    public Queue StreamParkingArea { get; }

    public DataConstruct(Construct scope, string id, DataConstructProps props) : base(scope, id)
    {
        OrdersTable = new Table(this, "orders", new TableProps
        {
            TableName = $"{props.Stage.StackId}-orders",
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute { Name = OrderAttributes.OrderId, Type = AttributeType.STRING },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            Stream = StreamViewType.NEW_AND_OLD_IMAGES,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        // The table is keyed for the one thing every worker has in hand, an order id. Showing the
        // newest orders is a second way in, and a scan would read the whole table to answer it
        // while still refusing to promise an order.
        OrdersTable.AddGlobalSecondaryIndex(new GlobalSecondaryIndexProps
        {
            IndexName = OrderIndexes.ByRecency,
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = OrderAttributes.RecencyBucket,
                Type = AttributeType.STRING
            },
            SortKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = OrderAttributes.RecencyCursor,
                Type = AttributeType.STRING
            },
            // A global index cannot reach back to the table for the attributes it left out, so
            // projecting less would buy a second round trip for every page.
            ProjectionType = ProjectionType.ALL
        });

        WorkParkingArea = new Queue(this, "work-parking", new QueueProps
        {
            QueueName = $"{props.Stage.StackId}-work-parking",
            VisibilityTimeout = FunctionDefaults.WorkVisibility,
            EnforceSSL = true,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        WorkQueue = new Queue(this, "work", new QueueProps
        {
            QueueName = $"{props.Stage.StackId}-work",
            VisibilityTimeout = FunctionDefaults.WorkVisibility,
            EnforceSSL = true,
            RemovalPolicy = RemovalPolicy.DESTROY,
            DeadLetterQueue = new DeadLetterQueue
            {
                Queue = WorkParkingArea,
                MaxReceiveCount = FunctionDefaults.RetryBudget
            }
        });

        ReviewQueue = new Queue(this, "review", new QueueProps
        {
            QueueName = $"{props.Stage.StackId}-review",
            VisibilityTimeout = FunctionDefaults.WorkVisibility,
            EnforceSSL = true,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        // Both things that read the change stream - the dispatcher and the routing rule - give up
        // on a record eventually, and until now the record simply vanished when they did. Neither
        // hands over an order id, so nothing consumes this queue; arriving here is the whole signal.
        StreamParkingArea = new Queue(this, "stream-parking", new QueueProps
        {
            QueueName = $"{props.Stage.StackId}-stream-parking",
            EnforceSSL = true,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        NagSuppressions.AddResourceSuppressions(ReviewQueue, new[]
        {
            new NagPackSuppression
            {
                Id = "AwsSolutions-SQS3",
                Reason = "Nothing in the review lane settles an order, so a message that cannot be "
                       + "handled has nothing to be parked for. It simply stays queued, and how "
                       + "long the oldest message has waited is what the review-queue-stale alarm "
                       + "watches."
            }
        });

        NagSuppressions.AddResourceSuppressions(WorkParkingArea, new[]
        {
            new NagPackSuppression
            {
                Id = "AwsSolutions-SQS3",
                Reason = "This queue is itself the parking area. Giving it one of its own would "
                       + "only move the question one queue further along."
            }
        });

        NagSuppressions.AddResourceSuppressions(StreamParkingArea, new[]
        {
            new NagPackSuppression
            {
                Id = "AwsSolutions-SQS3",
                Reason = "This queue is itself the parking area for everything that reads the "
                       + "change stream. Giving it one of its own would only move the question "
                       + "one queue further along."
            }
        });

        NagSuppressions.AddResourceSuppressions(OrdersTable, new[]
        {
            new NagPackSuppression
            {
                Id = "AwsSolutions-DDB3",
                Reason = "Point-in-time recovery is off on purpose. This table is created and "
                       + "thrown away with the stack it belongs to, so paying to make its contents "
                       + "recoverable would contradict the decision to discard them."
            }
        });
    }
}
