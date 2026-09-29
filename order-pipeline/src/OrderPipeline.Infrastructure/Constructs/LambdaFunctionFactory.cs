using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Constructs;
using OrderPipeline.Contracts;

namespace OrderPipeline.Infrastructure.Constructs;

/// <summary>
/// Builds the Functions project once and stamps out one Lambda per handler from that asset,
/// each with its own role and log group.
/// </summary>
internal sealed class LambdaFunctionFactory
{
    private const string FunctionsProject = "src/OrderPipeline.Functions";

    private readonly Construct _scope;

    private readonly StageConfig _stage;

    private readonly Code _code;

    public LambdaFunctionFactory(Construct scope, StageConfig stage)
    {
        _scope = scope;
        _stage = stage;
        _code = BuildFunctionsAsset();
    }

    public Function Create(string id, string handlerName, IDictionary<string, string> environment)
    {
        var functionName = $"{_stage.StackId}-{id}";

        var logGroup = new LogGroup(_scope, $"{id}-logs", new LogGroupProps
        {
            LogGroupName = $"/aws/lambda/{functionName}",
            Retention = RetentionDays.ONE_WEEK,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        var role = new Role(_scope, $"{id}-role", new RoleProps
        {
            AssumedBy = new ServicePrincipal("lambda.amazonaws.com")
        });

        logGroup.GrantWrite(role);

        environment[EnvironmentVariables.Handler] = handlerName;

        return new Function(_scope, id, new FunctionProps
        {
            FunctionName = functionName,
            Runtime = Runtime.DOTNET_10,
            Architecture = Architecture.X86_64,
            Handler = LambdaHandlers.Assembly,
            MemorySize = 512,
            Timeout = FunctionDefaults.Timeout,
            Role = role,
            LogGroup = logGroup,
            Environment = environment,
            Code = _code
        });
    }

    private static Code BuildFunctionsAsset() => Code.FromAsset(".", new Amazon.CDK.AWS.S3.Assets.AssetOptions
    {
        Exclude = new[]
        {
            "**/bin",
            "**/obj",
            "cdk.out",
            "tests",
            ".vs",
            ".aws-lambda-testtool",
            "src/OrderPipeline.AppHost",
            "src/OrderPipeline.Infrastructure",
            "frontend"
        },
        Bundling = new BundlingOptions
        {
            Image = Runtime.DOTNET_10.BundlingImage,
            User = "root",
            OutputType = BundlingOutput.NOT_ARCHIVED,
            Command = new[]
            {
                "/bin/sh",
                "-c",
                $"dotnet publish {FunctionsProject} -c Release -r linux-x64 --self-contained false -o /asset-output"
            }
        }
    });
}
