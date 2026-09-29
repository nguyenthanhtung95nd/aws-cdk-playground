using OpenTelemetry.Resources;

namespace OrderPipeline.ServiceDefaults;

internal static class TelemetryEnvironment
{
    private const string OtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";

    private const string ServiceName = "OTEL_SERVICE_NAME";

    // Only the local host sets an endpoint. Deployed functions export nothing and pay nothing.
    public static bool ExportsTelemetry => IsSet(OtlpEndpoint);

    public static ResourceBuilder ResourceFor(string fallbackServiceName) =>
        IsSet(ServiceName)
            ? ResourceBuilder.CreateDefault()
            : ResourceBuilder.CreateDefault().AddService(fallbackServiceName);

    private static bool IsSet(string variable) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable));
}
