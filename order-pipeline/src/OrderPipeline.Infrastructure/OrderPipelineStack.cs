using Amazon.CDK;
using Constructs;
using OrderPipeline.Infrastructure.Constructs;

namespace OrderPipeline.Infrastructure;

public sealed class OrderPipelineStackProps : StackProps
{
    public required StageConfig Stage { get; init; }
}

public sealed class OrderPipelineStack : Stack
{
    public OrderPipelineStack(Construct scope, string id, OrderPipelineStackProps props)
        : base(scope, id, props)
    {
        var data = new DataConstruct(this, "data", new DataConstructProps { Stage = props.Stage });

        var functions = new FunctionsConstruct(this, "functions", new FunctionsConstructProps
        {
            Stage = props.Stage,
            OrdersTable = data.OrdersTable,
            WorkQueue = data.WorkQueue,
            WorkParkingArea = data.WorkParkingArea,
            ReviewQueue = data.ReviewQueue,
            StreamParkingArea = data.StreamParkingArea
        });

        var api = new ApiConstruct(this, "api", new ApiConstructProps
        {
            Stage = props.Stage,
            GetHealth = functions.GetHealth,
            PlaceOrder = functions.PlaceOrder,
            ListOrders = functions.ListOrders
        });

        var routing = new ReviewRoutingConstruct(this, "review-routing", new ReviewRoutingConstructProps
        {
            Stage = props.Stage,
            OrdersTable = data.OrdersTable,
            ReviewQueue = data.ReviewQueue,
            StreamParkingArea = data.StreamParkingArea
        });

        _ = new MonitoringConstruct(this, "monitoring", new MonitoringConstructProps
        {
            Stage = props.Stage,
            Functions = functions.All,
            WorkQueue = data.WorkQueue,
            WorkParkingArea = data.WorkParkingArea,
            ReviewQueue = data.ReviewQueue,
            StreamParkingArea = data.StreamParkingArea,
            StreamDispatcher = functions.Dispatcher,
            ReviewRoutingPipeName = routing.PipeName
        });

        var web = new WebConstruct(this, "web", new WebConstructProps
        {
            Stage = props.Stage,
            HttpApi = api.HttpApi
        });

        new CfnOutput(this, "output-site-url", new CfnOutputProps
        {
            Value = $"https://{web.Distribution.DistributionDomainName}"
        });

        new CfnOutput(this, "output-api-endpoint", new CfnOutputProps
        {
            Value = api.HttpApi.ApiEndpoint
        });

        new CfnOutput(this, "output-orders-table", new CfnOutputProps
        {
            Value = data.OrdersTable.TableName
        });

        new CfnOutput(this, "output-work-queue", new CfnOutputProps
        {
            Value = data.WorkQueue.QueueUrl
        });

        new CfnOutput(this, "output-work-parking", new CfnOutputProps
        {
            Value = data.WorkParkingArea.QueueUrl
        });

        new CfnOutput(this, "output-review-queue", new CfnOutputProps
        {
            Value = data.ReviewQueue.QueueUrl
        });

        new CfnOutput(this, "output-stream-parking", new CfnOutputProps
        {
            Value = data.StreamParkingArea.QueueUrl
        });

        Amazon.CDK.Tags.Of(this).Add("System", props.Stage.TagSystem);
        Amazon.CDK.Tags.Of(this).Add("Environment", props.Stage.TagEnvironment);
        Amazon.CDK.Tags.Of(this).Add("SystemApp", props.Stage.TagSystemApp);
        Amazon.CDK.Tags.Of(this).Add("CustomerCode", props.Stage.TagCustomerCode);
    }
}
