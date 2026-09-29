using Amazon.CDK.Assertions;

namespace OrderPipeline.Infrastructure.Tests;

public class NagComplianceTests
{
    [Fact]
    public void Synthesize_Always_LeavesNoUnsuppressedNagFinding()
    {
        var annotations = Annotations.FromStack(StackTemplate.StackWith());

        annotations.HasNoError("*", Match.StringLikeRegexp("AwsSolutions-.*"));
        annotations.HasNoWarning("*", Match.StringLikeRegexp("AwsSolutions-.*"));
    }
}
