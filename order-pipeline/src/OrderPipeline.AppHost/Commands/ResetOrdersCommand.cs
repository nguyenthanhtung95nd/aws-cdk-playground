using Aspire.Hosting.AWS.DynamoDB;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OrderPipeline.AppHost.Commands;

internal static class ResetOrdersCommand
{
    public static IResourceBuilder<DynamoDBLocalResource> WithResetOrdersCommand(
        this IResourceBuilder<DynamoDBLocalResource> dynamo,
        string tableName) =>
        dynamo.WithCommand(
            name: "reset-orders",
            displayName: "Reset orders table",
            executeCommand: async context =>
            {
                var endpointUrl = await dynamo.Resource.GetEndpoints().Single().GetValueAsync(context.CancellationToken)
                    ?? throw new InvalidOperationException("DynamoDB Local has no endpoint.");

                await LocalOrdersTable.ResetAsync(endpointUrl, tableName, context.CancellationToken);
                return CommandResults.Success();
            },
            commandOptions: new CommandOptions
            {
                Description = "Drops every order and recreates the empty table.",
                ConfirmationMessage = "All local orders will be lost. Continue?",
                IconName = "Delete",
                IconVariant = IconVariant.Filled,
                UpdateState = context => context.ResourceSnapshot.HealthStatus == HealthStatus.Healthy
                    ? ResourceCommandState.Enabled
                    : ResourceCommandState.Disabled
            });
}
