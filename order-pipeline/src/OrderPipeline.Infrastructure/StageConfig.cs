using System.Globalization;
using System.Text.Json;
using Amazon.CDK;
using OrderPipeline.Domain.Orders;

namespace OrderPipeline.Infrastructure;

public sealed record StageConfig(
    string TagSystem,
    string TagEnvironment,
    string TagSystemApp,
    string TagCustomerCode,
    string Region,
    string AlertEmail,
    OrderThresholds Thresholds)
{
    public string StackId => $"{TagSystem}-{TagEnvironment}-{TagCustomerCode}";

    public string HighValueThreshold => Thresholds.HighValue.ToString(CultureInfo.InvariantCulture);

    public string BusinessLimit => Thresholds.BusinessLimit.ToString(CultureInfo.InvariantCulture);

    public static StageConfig FromContext(App app)
    {
        var stageName = app.Node.TryGetContext("stage") as string ?? "dev";

        if (app.Node.TryGetContext("stages") is not IDictionary<string, object> stages
            || !stages.TryGetValue(stageName, out var entry)
            || entry is not IDictionary<string, object> stage)
        {
            throw new InvalidOperationException(
                $"Unknown stage '{stageName}'. Add it under context.stages in cdk.json.");
        }

        return FromStage(
            stage,
            app.Node.TryGetContext("customerCode") as string ?? Read(stage, "tagCustomerCode"));
    }

    public static StageConfig FromCdkJson(string cdkJsonPath, string stageName)
    {
        if (!File.Exists(cdkJsonPath))
        {
            throw new FileNotFoundException("cdk.json is the source for stage config.", cdkJsonPath);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(cdkJsonPath));

        if (!document.RootElement.GetProperty("context").GetProperty("stages")
                .TryGetProperty(stageName, out var entry))
        {
            throw new InvalidOperationException(
                $"Unknown stage '{stageName}'. Add it under context.stages in '{cdkJsonPath}'.");
        }

        var stage = entry.EnumerateObject()
            .ToDictionary(field => field.Name, field => (object)(field.Value.GetString() ?? string.Empty));

        return FromStage(stage, Read(stage, "tagCustomerCode"));
    }

    private static StageConfig FromStage(IDictionary<string, object> stage, string customerCode) =>
        new(Read(stage, "tagSystem"),
            Read(stage, "tagEnvironment"),
            Read(stage, "tagSystemApp"),
            customerCode,
            Read(stage, "region"),
            ReadOptional(stage, "alertEmail"),
            new OrderThresholds(
                ReadDecimal(stage, "highValueThreshold"),
                ReadDecimal(stage, "businessLimit")));

    private static string Read(IDictionary<string, object> stage, string key) =>
        stage.TryGetValue(key, out var value) && value is string text && text.Length > 0
            ? text
            : throw new InvalidOperationException($"Stage config is missing '{key}'.");

    private static string ReadOptional(IDictionary<string, object> stage, string key) =>
        stage.TryGetValue(key, out var value) && value is string text ? text : string.Empty;

    private static decimal ReadDecimal(IDictionary<string, object> stage, string key) =>
        decimal.TryParse(Read(stage, key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException($"Stage config value '{key}' must be a decimal number.");
}
