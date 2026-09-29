namespace OrderPipeline.Contracts;

public static class EnvironmentVariables
{
    public const string Handler = "ORDER_PIPELINE_HANDLER";
    public const string OrdersTableName = "ORDERS_TABLE_NAME";
    public const string WorkQueueUrl = "WORK_QUEUE_URL";
    public const string HighValueThreshold = "HIGH_VALUE_THRESHOLD";
    public const string BusinessLimit = "BUSINESS_LIMIT";
}
