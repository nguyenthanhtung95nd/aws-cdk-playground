using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.SQS;
using Amazon.Lambda.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderPipeline.Contracts;
using OrderPipeline.Domain.Orders;
using OrderPipeline.Domain.Ports;
using OrderPipeline.Functions.Adapters;
using OrderPipeline.ServiceDefaults;

namespace OrderPipeline.Functions;

[LambdaStartup]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddLambdaServiceDefaults(Read(EnvironmentVariables.Handler));

        services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient());
        services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient());

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IOrderIdGenerator, GuidOrderIdGenerator>();

        services.AddSingleton(_ => new OrderThresholds(
            ReadDecimal(EnvironmentVariables.HighValueThreshold),
            ReadDecimal(EnvironmentVariables.BusinessLimit)));

        services.AddSingleton<IOrderStore>(provider => new DynamoDbOrderStore(
            provider.GetRequiredService<IAmazonDynamoDB>(),
            Read(EnvironmentVariables.OrdersTableName),
            provider.GetRequiredService<ILogger<DynamoDbOrderStore>>()));

        services.AddSingleton<IOrderWorkQueue>(provider => new SqsOrderWorkQueue(
            provider.GetRequiredService<IAmazonSQS>(),
            Read(EnvironmentVariables.WorkQueueUrl)));
    }

    private static string Read(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Environment variable '{name}' is required.");

    private static decimal ReadDecimal(string name) =>
        decimal.TryParse(Read(name), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException($"Environment variable '{name}' must be a decimal number.");
}
