using System.Net.Http.Json;
using Aspire.Hosting.AWS.Lambda;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderPipeline.Contracts;

namespace OrderPipeline.AppHost.Commands;

internal static class SeedOrdersCommand
{
    private sealed record SampleOrder(string CustomerName, string Product, decimal Amount, string Notes);

    // One of them crosses the high-value threshold from cdk.json so both tiers show up.
    private static readonly SampleOrder[] Samples =
    [
        new("Alice", "Headphones", 1499, ""),
        new("Bob", "Standing desk", 12000, "Ships in two parts"),
        new("Carol", "Keyboard", 250, "")
    ];

    public static IResourceBuilder<APIGatewayEmulatorResource> WithSeedOrdersCommand(
        this IResourceBuilder<APIGatewayEmulatorResource> gateway) =>
        gateway.WithCommand(
            name: "seed-orders",
            displayName: "Seed sample orders",
            executeCommand: async context =>
            {
                var baseUrl = await gateway.GetEndpoint("http").GetValueAsync(context.CancellationToken)
                    ?? throw new InvalidOperationException("The API Gateway emulator has no endpoint.");

                using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

                foreach (var sample in Samples)
                {
                    var response = await http.PostAsJsonAsync(ApiRoutes.Orders, sample, context.CancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        return CommandResults.Failure($"Placing {sample.Product} returned {(int)response.StatusCode}.");
                    }
                }

                return CommandResults.Success();
            },
            commandOptions: new CommandOptions
            {
                Description = $"Places {Samples.Length} sample orders through the API.",
                IconName = "AddCircle",
                IconVariant = IconVariant.Filled,
                UpdateState = context => context.ResourceSnapshot.HealthStatus == HealthStatus.Healthy
                    ? ResourceCommandState.Enabled
                    : ResourceCommandState.Disabled
            });
}
