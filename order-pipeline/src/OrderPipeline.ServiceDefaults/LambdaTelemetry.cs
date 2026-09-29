using System.Diagnostics;
using Amazon.Lambda.Core;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OrderPipeline.ServiceDefaults;

/// <summary>
/// Traces and metrics for one Lambda process. Built by hand rather than through the host
/// builder because Lambda Annotations never starts hosted services.
/// </summary>
public sealed class LambdaTelemetry : IDisposable
{
    public static readonly ActivitySource Source = new("OrderPipeline");

    private readonly TracerProvider? _tracing;

    private readonly MeterProvider? _metrics;

    private LambdaTelemetry(TracerProvider? tracing, MeterProvider? metrics)
    {
        _tracing = tracing;
        _metrics = metrics;
    }

    public static LambdaTelemetry Start(string serviceName)
    {
        if (!TelemetryEnvironment.ExportsTelemetry)
        {
            return new LambdaTelemetry(null, null);
        }

        var resource = TelemetryEnvironment.ResourceFor(serviceName);

        var tracing = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resource)
            .AddSource(Source.Name)
            .AddAWSLambdaConfigurations(options => options.DisableAwsXRayContextExtraction = true)
            .AddAWSInstrumentation()
            .AddOtlpExporter()
            .Build();

        var metrics = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resource)
            .AddRuntimeInstrumentation()
            .AddOtlpExporter()
            .Build();

        return new LambdaTelemetry(tracing, metrics);
    }

    public Task<TResult> TraceAsync<TInput, TResult>(
        Func<TInput, ILambdaContext, Task<TResult>> handler,
        TInput input,
        ILambdaContext context) =>
        _tracing is null
            ? handler(input, context)
            : AWSLambdaWrapper.TraceAsync(_tracing, handler, input, context);

    public void Dispose()
    {
        _tracing?.Dispose();
        _metrics?.Dispose();
    }
}
