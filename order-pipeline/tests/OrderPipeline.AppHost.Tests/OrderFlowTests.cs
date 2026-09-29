using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using OrderPipeline.Contracts;
using OrderPipeline.Functions.Responses;

namespace OrderPipeline.AppHost.Tests;

[Trait("Category", "Aspire")]
public class OrderFlowTests
{
    private const string Gateway = "ApiGateway";

    // First run pulls the DynamoDB Local image and installs the Lambda test tool.
    private static readonly TimeSpan StartupBudget = TimeSpan.FromMinutes(5);

    [AspireFact]
    public async Task PlaceOrder_ThroughTheRunningHost_IsListedAsPending()
    {
        using var budget = new CancellationTokenSource(StartupBudget);
        var token = budget.Token;

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.OrderPipeline_AppHost>(token);
        await using var app = await appHost.BuildAsync(token);
        await app.StartAsync(token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync(Gateway, token);

        using var http = app.CreateHttpClient(Gateway);

        var placed = await http.PostAsJsonAsync(
            ApiRoutes.Orders,
            new { customerName = "Aspire test", product = "Smoke", amount = 42, notes = "" },
            token);
        placed.EnsureSuccessStatusCode();

        var receipt = await placed.Content.ReadFromJsonAsync<PlaceOrderResponse>(token);
        var page = await http.GetFromJsonAsync<OrderPageResponse>(ApiRoutes.Orders, token);

        Assert.NotNull(receipt);
        Assert.NotNull(page);
        Assert.Contains(page.Orders, order => order.OrderId == receipt.OrderId && order.Status == "PENDING");
    }
}
