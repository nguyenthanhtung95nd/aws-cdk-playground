using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using OrderPipeline.Contracts;

namespace OrderPipeline.AppHost;

/// <summary>
/// Creates the orders table in DynamoDB Local, which starts empty on every run.
/// Mirrors the table in DataConstruct; change one, change the other.
/// </summary>
internal static class LocalOrdersTable
{
    public static async Task EnsureAsync(string endpointUrl, string tableName, CancellationToken cancellationToken)
    {
        using var client = Client(endpointUrl);

        if (await ExistsAsync(client, tableName, cancellationToken))
        {
            return;
        }

        await client.CreateTableAsync(Definition(tableName), cancellationToken);
    }

    public static async Task ResetAsync(string endpointUrl, string tableName, CancellationToken cancellationToken)
    {
        using var client = Client(endpointUrl);

        if (await ExistsAsync(client, tableName, cancellationToken))
        {
            await client.DeleteTableAsync(tableName, cancellationToken);
        }

        await client.CreateTableAsync(Definition(tableName), cancellationToken);
    }

    private static AmazonDynamoDBClient Client(string endpointUrl) => new(
        new BasicAWSCredentials(AwsSdkVariables.LocalCredential, AwsSdkVariables.LocalCredential),
        new AmazonDynamoDBConfig { ServiceURL = endpointUrl });

    private static async Task<bool> ExistsAsync(AmazonDynamoDBClient client, string tableName, CancellationToken cancellationToken)
    {
        var existing = await client.ListTablesAsync(cancellationToken);
        return existing.TableNames.Contains(tableName);
    }

    private static CreateTableRequest Definition(string tableName) => new()
    {
        TableName = tableName,
        BillingMode = BillingMode.PAY_PER_REQUEST,
        AttributeDefinitions =
        [
            new AttributeDefinition(OrderAttributes.OrderId, ScalarAttributeType.S),
            new AttributeDefinition(OrderAttributes.RecencyBucket, ScalarAttributeType.S),
            new AttributeDefinition(OrderAttributes.RecencyCursor, ScalarAttributeType.S)
        ],
        KeySchema = [new KeySchemaElement(OrderAttributes.OrderId, KeyType.HASH)],
        GlobalSecondaryIndexes =
        [
            new GlobalSecondaryIndex
            {
                IndexName = OrderIndexes.ByRecency,
                KeySchema =
                [
                    new KeySchemaElement(OrderAttributes.RecencyBucket, KeyType.HASH),
                    new KeySchemaElement(OrderAttributes.RecencyCursor, KeyType.RANGE)
                ],
                Projection = new Projection { ProjectionType = ProjectionType.ALL }
            }
        ],
        StreamSpecification = new StreamSpecification
        {
            StreamEnabled = true,
            StreamViewType = StreamViewType.NEW_AND_OLD_IMAGES
        }
    };
}
