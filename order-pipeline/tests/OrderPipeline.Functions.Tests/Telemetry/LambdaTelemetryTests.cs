using Amazon.Lambda.Core;
using NSubstitute;
using OrderPipeline.ServiceDefaults;

namespace OrderPipeline.Functions.Tests.Telemetry;

public class LambdaTelemetryTests
{
    [Fact]
    public async Task TraceAsync_WithoutAnExporterEndpoint_RunsTheHandlerUnchanged()
    {
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", null);
        using var telemetry = LambdaTelemetry.Start("test");

        var result = await telemetry.TraceAsync(
            (string input, ILambdaContext _) => Task.FromResult(input + "!"),
            "hello",
            Substitute.For<ILambdaContext>());

        Assert.Equal("hello!", result);
    }

    [Fact]
    public async Task TraceAsync_WithoutAnExporterEndpoint_StartsNoActivity()
    {
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", null);
        using var telemetry = LambdaTelemetry.Start("test");

        var current = await telemetry.TraceAsync(
            (string _, ILambdaContext _) => Task.FromResult(System.Diagnostics.Activity.Current),
            "hello",
            Substitute.For<ILambdaContext>());

        Assert.Null(current);
    }
}
