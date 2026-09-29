using Amazon.CDK.Assertions;
using OrderPipeline.Contracts;

namespace OrderPipeline.Infrastructure.Tests.Constructs;

public class DataConstructTests
{
    [Fact]
    public void Synthesize_Always_KeysTheTableByOrderId()
    {
        var template = StackTemplate.SynthesizeWith();

        template.HasResourceProperties("AWS::DynamoDB::Table", Match.ObjectLike(new Dictionary<string, object>
        {
            ["TableName"] = "orderpipeline-dev-demo-orders",
            ["KeySchema"] = new[]
            {
                new Dictionary<string, object> { ["AttributeName"] = "orderId", ["KeyType"] = "HASH" }
            }
        }));
    }

    [Fact]
    public void Synthesize_Always_StreamsBothImages()
    {
        var template = StackTemplate.SynthesizeWith();

        template.HasResourceProperties("AWS::DynamoDB::Table", Match.ObjectLike(new Dictionary<string, object>
        {
            ["StreamSpecification"] = new Dictionary<string, object>
            {
                ["StreamViewType"] = "NEW_AND_OLD_IMAGES"
            }
        }));
    }

    [Fact]
    public void Synthesize_Always_HidesAQueuedMessageForSixInvocationsWorthOfTime()
    {
        var template = StackTemplate.SynthesizeWith();

        template.HasResourceProperties("AWS::SQS::Queue", Match.ObjectLike(new Dictionary<string, object>
        {
            ["QueueName"] = "orderpipeline-dev-demo-work",
            ["VisibilityTimeout"] = 180
        }));
    }

    [Fact]
    public void Synthesize_Always_ParksAMessageAfterThreeAttempts()
    {
        var template = StackTemplate.SynthesizeWith();

        template.HasResourceProperties("AWS::SQS::Queue", Match.ObjectLike(new Dictionary<string, object>
        {
            ["QueueName"] = "orderpipeline-dev-demo-work",
            ["RedrivePolicy"] = Match.ObjectLike(new Dictionary<string, object>
            {
                ["maxReceiveCount"] = 3
            })
        }));
    }

    [Fact]
    public void Synthesize_Always_GivesTheParkingAreaTheSameHoldAsTheWorkQueue()
    {
        var template = StackTemplate.SynthesizeWith();

        template.ResourceCountIs("AWS::SQS::Queue", 4);
        template.HasResourceProperties("AWS::SQS::Queue", Match.ObjectLike(new Dictionary<string, object>
        {
            ["QueueName"] = "orderpipeline-dev-demo-work-parking",
            ["VisibilityTimeout"] = 180,
            ["RedrivePolicy"] = Match.Absent()
        }));
    }

    [Fact]
    public void Synthesize_Always_GivesTheChangeStreamItsOwnParkingArea()
    {
        var template = StackTemplate.SynthesizeWith();

        template.HasResourceProperties("AWS::SQS::Queue", Match.ObjectLike(new Dictionary<string, object>
        {
            ["QueueName"] = "orderpipeline-dev-demo-stream-parking",
            ["RedrivePolicy"] = Match.Absent()
        }));
    }

    [Fact]
    public void Synthesize_Always_RefusesUnencryptedTrafficToTheWorkQueue()
    {
        var template = StackTemplate.SynthesizeWith();

        var policies = template.FindResources("AWS::SQS::QueuePolicy").Values
            .Select(policy => System.Text.Json.JsonSerializer.Serialize(policy))
            .ToList();

        Assert.Contains(policies, policy => policy.Contains("aws:SecureTransport"));
    }

    // Without this index the newest orders can only be found by reading every order there is.
    [Fact]
    public void Synthesize_Always_KeepsTheOrdersSortedByWhenTheyWerePlaced()
    {
        var template = StackTemplate.SynthesizeWith();

        template.HasResourceProperties("AWS::DynamoDB::Table", Match.ObjectLike(new Dictionary<string, object>
        {
            ["GlobalSecondaryIndexes"] = Match.ArrayWith([
                Match.ObjectLike(new Dictionary<string, object>
                {
                    ["IndexName"] = OrderIndexes.ByRecency,
                    ["KeySchema"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["AttributeName"] = OrderAttributes.RecencyBucket,
                            ["KeyType"] = "HASH"
                        },
                        new Dictionary<string, object>
                        {
                            ["AttributeName"] = OrderAttributes.RecencyCursor,
                            ["KeyType"] = "RANGE"
                        }
                    },
                    // A global index cannot reach back to the table, so anything left out here
                    // would cost a second round trip for every page.
                    ["Projection"] = new Dictionary<string, object> { ["ProjectionType"] = "ALL" }
                })
            ])
        }));
    }

    [Fact]
    public void Synthesize_Always_LetsTheTableGoWithTheStack()
    {
        var template = StackTemplate.SynthesizeWith();

        template.HasResource("AWS::DynamoDB::Table", Match.ObjectLike(new Dictionary<string, object>
        {
            ["DeletionPolicy"] = "Delete"
        }));
    }
}
