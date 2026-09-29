namespace OrderPipeline.Infrastructure.Tests;

public class OrderPipelineStackTests
{
    [Fact]
    public void Synthesize_WithCustomerCodeOverride_NamesResourcesApart()
    {
        StackTemplate.SynthesizeWith().HasResourceProperties("AWS::Lambda::Function", new Dictionary<string, object>
        {
            ["FunctionName"] = "orderpipeline-dev-demo-place-order"
        });

        StackTemplate.SynthesizeWith("mr42").HasResourceProperties("AWS::Lambda::Function", new Dictionary<string, object>
        {
            ["FunctionName"] = "orderpipeline-dev-mr42-place-order"
        });
    }

    [Fact]
    public void Synthesize_Always_TagsEveryResourceWithTheStage()
    {
        var template = StackTemplate.SynthesizeWith();

        template.HasResourceProperties("AWS::DynamoDB::Table", Amazon.CDK.Assertions.Match.ObjectLike(
            new Dictionary<string, object>
            {
                ["Tags"] = Amazon.CDK.Assertions.Match.ArrayWith(new object[]
                {
                    new Dictionary<string, object> { ["Key"] = "System", ["Value"] = "orderpipeline" }
                })
            }));
    }
}
