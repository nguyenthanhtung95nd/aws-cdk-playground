using Aspire.Hosting.AWS.DynamoDB;

namespace OrderPipeline.AppHost;

/// <summary>
/// A browser UI over DynamoDB Local, shown on the dashboard next to the database it reads.
/// </summary>
internal static class DynamoDbAdmin
{
    private const string Image = "aaronshaf/dynamodb-admin";

    private const string Tag = "5.3.4";

    private const int ContainerPort = 8001;

    public static IResourceBuilder<ContainerResource> AddDynamoDbAdmin(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<DynamoDBLocalResource> dynamo,
        string region) =>
        builder.AddContainer("dynamodb-admin", Image, Tag)
            .WithHttpEndpoint(targetPort: ContainerPort, name: "http")
            .WithEnvironment("DYNAMO_ENDPOINT", dynamo.Resource.GetEndpoints().Single())
            .WithEnvironment(AwsSdkVariables.Region, region)
            .WithEnvironment(AwsSdkVariables.AccessKeyId, AwsSdkVariables.LocalCredential)
            .WithEnvironment(AwsSdkVariables.SecretAccessKey, AwsSdkVariables.LocalCredential)
            .WithParentRelationship(dynamo)
            .WaitFor(dynamo);
}
