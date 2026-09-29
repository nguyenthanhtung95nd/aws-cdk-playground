using Amazon.CDK;
using Amazon.CDK.Assertions;
using Cdklabs.CdkNag;
using OrderPipeline.Infrastructure;

namespace OrderPipeline.Infrastructure.Tests;

public static class StackTemplate
{
    // Asset paths in the constructs are relative, and the CDK CLI runs the app from the folder
    // holding cdk.json. A test run starts in its own output folder instead, so those paths would
    // point somewhere that does not exist. Standing where cdk stands makes the two agree.
    static StackTemplate() => Directory.SetCurrentDirectory(AppRoot());

    private static string AppRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "cdk.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException(
                "No cdk.json above the test output folder, so the app root cannot be located.");
    }

    // Uploading the site adds a Lambda that CDK writes, owns and runs on its own runtime. Asking
    // "every function" would now include it and answer for decisions this stack never made.
    public static IReadOnlyList<IDictionary<string, object>> OurFunctions(Template template) =>
        template.FindResources("AWS::Lambda::Function")
            .Values
            .Select(resource => (IDictionary<string, object>)
                ((IDictionary<string, object>)resource!)["Properties"])
            .Where(properties => properties.TryGetValue("Handler", out var handler)
                                 && handler as string == "OrderPipeline.Functions")
            .ToList();

    public static Dictionary<string, object> ContextFor(
        string? customerCode = null,
        string highValueThreshold = "10000",
        string businessLimit = "50000",
        string alertEmail = "")
    {
        var context = new Dictionary<string, object>
        {
            ["aws:cdk:bundling-stacks"] = Array.Empty<string>(),
            ["stages"] = new Dictionary<string, object>
            {
                ["dev"] = new Dictionary<string, object>
                {
                    ["tagSystem"] = "orderpipeline",
                    ["tagEnvironment"] = "dev",
                    ["tagSystemApp"] = "orders",
                    ["tagCustomerCode"] = "demo",
                    ["region"] = "us-west-1",
                    ["alertEmail"] = alertEmail,
                    ["highValueThreshold"] = highValueThreshold,
                    ["businessLimit"] = businessLimit
                }
            }
        };

        if (customerCode is not null)
        {
            context["customerCode"] = customerCode;
        }

        return context;
    }

    public static Template SynthesizeWith(string? customerCode = null, string alertEmail = "") =>
        Template.FromStack(StackWith(customerCode, alertEmail));

    public static Stack StackWith(string? customerCode = null, string alertEmail = "")
    {
        var app = new App(new AppProps { Context = ContextFor(customerCode, alertEmail: alertEmail) });
        Aspects.Of(app).Add(new AwsSolutionsChecks(new NagPackProps { Verbose = true }));

        var stage = StageConfig.FromContext(app);

        return new OrderPipelineStack(app, stage.StackId, new OrderPipelineStackProps
        {
            Env = new Amazon.CDK.Environment { Account = "123456789012", Region = stage.Region },
            Stage = stage
        });
    }
}
