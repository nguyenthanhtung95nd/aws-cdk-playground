using Amazon.CDK;
using OrderPipeline.Infrastructure;

namespace OrderPipeline.Infrastructure.Tests;

public class StageConfigTests
{
    [Fact]
    public void FromContext_WithUnknownStage_Throws()
    {
        var context = StackTemplate.ContextFor();
        context["stage"] = "nope";
        var app = new App(new AppProps { Context = context });

        var error = Assert.Throws<InvalidOperationException>(() => StageConfig.FromContext(app));

        Assert.Contains("nope", error.Message);
    }

    [Fact]
    public void FromContext_WhenBusinessLimitIsNotAboveHighValue_FailsAtSynthesis()
    {
        var app = new App(new AppProps
        {
            Context = StackTemplate.ContextFor(highValueThreshold: "50000", businessLimit: "10000")
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => StageConfig.FromContext(app));
    }

    [Fact]
    public void FromContext_WithNonNumericThreshold_FailsAtSynthesis()
    {
        var app = new App(new AppProps
        {
            Context = StackTemplate.ContextFor(highValueThreshold: "ten thousand")
        });

        var error = Assert.Throws<InvalidOperationException>(() => StageConfig.FromContext(app));

        Assert.Contains("highValueThreshold", error.Message);
    }

    [Fact]
    public void StackId_WithCustomerCodeOverride_IsolatesTheStack()
    {
        var app = new App(new AppProps { Context = StackTemplate.ContextFor("mr42") });

        Assert.Equal("orderpipeline-dev-mr42", StageConfig.FromContext(app).StackId);
    }

    [Fact]
    public void FromCdkJson_ForTheStageTheDeploymentUses_ReadsTheSameValues()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "cdk.json"));

        var stage = StageConfig.FromCdkJson(path, "dev");

        Assert.Equal("orderpipeline", stage.TagSystem);
        Assert.Equal("dev", stage.TagEnvironment);
        Assert.Equal("orders", stage.TagSystemApp);
        Assert.Equal("demo", stage.TagCustomerCode);
        Assert.Equal("us-west-1", stage.Region);
        Assert.Equal(10_000m, stage.Thresholds.HighValue);
        Assert.Equal(50_000m, stage.Thresholds.BusinessLimit);
    }

    [Fact]
    public void FromCdkJson_ForAnUnknownStage_SaysWhereToAddIt()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "cdk.json"));

        var error = Assert.Throws<InvalidOperationException>(() => StageConfig.FromCdkJson(path, "nope"));

        Assert.Contains("context.stages", error.Message);
    }

    [Fact]
    public void FromCdkJson_WhenTheFileIsMissing_NamesTheFileItLookedFor()
    {
        var missing = Path.Combine(AppContext.BaseDirectory, "no-such-cdk.json");

        var error = Assert.Throws<FileNotFoundException>(() => StageConfig.FromCdkJson(missing, "dev"));

        Assert.Equal(missing, error.FileName);
    }
}
