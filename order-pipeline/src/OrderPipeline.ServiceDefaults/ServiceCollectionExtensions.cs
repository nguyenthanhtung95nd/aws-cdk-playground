using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace OrderPipeline.ServiceDefaults;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLambdaServiceDefaults(this IServiceCollection services, string serviceName) =>
        services.AddLogging(logging =>
        {
            // CloudWatch reads stdout, so the console stays on even when logs are also exported.
            logging.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.UseUtcTimestamp = true;
            });

            if (!TelemetryEnvironment.ExportsTelemetry)
            {
                return;
            }

            logging.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.IncludeFormattedMessage = true;
                options.SetResourceBuilder(TelemetryEnvironment.ResourceFor(serviceName));
                options.AddOtlpExporter();
            });
        });
}
