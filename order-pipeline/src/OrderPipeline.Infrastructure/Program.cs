using Amazon.CDK;
using Cdklabs.CdkNag;
using OrderPipeline.Infrastructure;

var app = new App();

Aspects.Of(app).Add(new AwsSolutionsChecks(new NagPackProps { Verbose = true }));

var stage = StageConfig.FromContext(app);

new OrderPipelineStack(app, stage.StackId, new OrderPipelineStackProps
{
    Env = new Amazon.CDK.Environment
    {
        Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
        Region = stage.Region
    },
    Description = "Order Pipeline - event-driven order processing",
    Stage = stage
});

app.Synth();
