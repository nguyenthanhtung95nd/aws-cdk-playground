using Amazon.CDK.Assertions;
using OrderPipeline.Contracts;

namespace OrderPipeline.Infrastructure.Tests.Constructs;

public class FunctionsConstructTests
{
    [Fact]
    public void Synthesize_Always_RunsEveryFunctionOnDotnet10()
    {
        var functions = StackTemplate.OurFunctions(StackTemplate.SynthesizeWith());

        Assert.Equal(7, functions.Count);
        Assert.All(functions, function =>
        {
            Assert.Equal("dotnet10", function["Runtime"]);
            Assert.Equal(new[] { "x86_64" }, function["Architectures"]);
        });
    }

    [Fact]
    public void Synthesize_Always_SelectsEachHandlerByEnvironmentVariable()
    {
        var template = StackTemplate.SynthesizeWith();

        foreach (var handler in new[] { "GetHealth", "PlaceOrder", "ListOrders" })
        {
            template.HasResourceProperties("AWS::Lambda::Function", Match.ObjectLike(new Dictionary<string, object>
            {
                ["Environment"] = Match.ObjectLike(new Dictionary<string, object>
                {
                    ["Variables"] = Match.ObjectLike(new Dictionary<string, object>
                    {
                        ["ORDER_PIPELINE_HANDLER"] = handler
                    })
                })
            }));
        }
    }

    [Fact]
    public void Synthesize_Always_ShipsOneAssetForEveryFunction()
    {
        var template = StackTemplate.SynthesizeWith();

        var keys = StackTemplate.OurFunctions(template)
            .Select(function => ((IDictionary<string, object>)function["Code"])["S3Key"])
            .Distinct()
            .ToList();

        Assert.Single(keys);
    }

    [Fact]
    public void Synthesize_Always_GivesEachFunctionItsOwnRoleAndLogGroup()
    {
        var template = StackTemplate.SynthesizeWith();
        var functions = StackTemplate.OurFunctions(template);

        var roles = functions
            .Select(function => System.Text.Json.JsonSerializer.Serialize(function["Role"]))
            .Distinct()
            .Count();

        var functionLogGroups = template.FindResources("AWS::Logs::LogGroup").Values
            .Select(group => System.Text.Json.JsonSerializer.Serialize(group))
            .Count(group => group.Contains("/aws/lambda/"));

        Assert.Equal(functions.Count, roles);
        Assert.Equal(functions.Count, functionLogGroups);
    }

    [Fact]
    public void Synthesize_Always_WritesTheRightsItGrantsRatherThanBorrowingAManagedPolicy()
    {
        var template = StackTemplate.SynthesizeWith();

        var borrowed = template.FindResources("AWS::IAM::Role")
            .Where(role => System.Text.Json.JsonSerializer.Serialize(role.Value).Contains("ManagedPolicyArns"))
            .Select(role => role.Key)
            .ToList();

        // The exceptions are the custom resources CDK writes for itself, to upload the site and
        // to empty the bucket on destroy. Their roles are not ours to shape.
        Assert.NotEmpty(borrowed);
        Assert.All(borrowed, key => Assert.StartsWith("Custom", key));
    }

    [Fact]
    public void Synthesize_Always_KeepsTableConfigurationOffTheHealthFunction()
    {
        var template = StackTemplate.SynthesizeWith();

        var health = template.FindResources("AWS::Lambda::Function", Match.ObjectLike(new Dictionary<string, object>
        {
            ["Properties"] = Match.ObjectLike(new Dictionary<string, object>
            {
                ["FunctionName"] = "orderpipeline-dev-demo-get-health"
            })
        }));

        var variables = (IDictionary<string, object>)
            ((IDictionary<string, object>)((IDictionary<string, object>)
                ((IDictionary<string, object>)health.Values.Single()!)["Properties"])["Environment"])["Variables"];

        Assert.DoesNotContain("ORDERS_TABLE_NAME", variables.Keys);
    }

    [Fact]
    public void Synthesize_Always_SplitsReadAndWriteAcrossDifferentFunctions()
    {
        var template = StackTemplate.SynthesizeWith();

        var policies = template.FindResources("AWS::IAM::Policy").Values
            .Select(policy => System.Text.Json.JsonSerializer.Serialize(policy))
            .ToList();

        Assert.Contains(policies, policy => policy.Contains("dynamodb:PutItem"));
        Assert.Contains(policies, policy => policy.Contains("dynamodb:Query"));

        Assert.DoesNotContain(policies, policy =>
            policy.Contains("dynamodb:Query") && policy.Contains("dynamodb:PutItem"));
    }

    // Reading the whole table is no longer how this system answers "the newest orders", so nothing
    // should still be allowed to do it. A scan left permitted is a scan somebody will write.
    [Fact]
    public void Synthesize_Always_LeavesNobodyAbleToReadTheWholeTable()
    {
        var policies = StackTemplate.SynthesizeWith().FindResources("AWS::IAM::Policy").Values
            .Select(policy => System.Text.Json.JsonSerializer.Serialize(policy));

        Assert.DoesNotContain(policies, policy => policy.Contains("dynamodb:Scan"));
    }

    // Table.Grant widens every grant to the indexes as well once the table has one, which would
    // hand four functions that never touch the recency index the right to read it.
    [Fact]
    public void Synthesize_Always_LetsOnlyTheListingReachTheRecencyIndex()
    {
        var policies = StackTemplate.SynthesizeWith().FindResources("AWS::IAM::Policy").Values
            .Select(policy => System.Text.Json.JsonSerializer.Serialize(policy))
            .Where(policy => policy.Contains("/index/"))
            .ToList();

        var onlyOne = Assert.Single(policies);
        Assert.Contains($"/index/{OrderIndexes.ByRecency}", onlyOne);
        Assert.DoesNotContain("/index/*", onlyOne);
    }

    [Fact]
    public void Synthesize_Always_LetsOnlyTheDispatcherAndTheRoutingRuleReachTheChangeStream()
    {
        var template = StackTemplate.SynthesizeWith();

        var readers = template.FindResources("AWS::IAM::Policy")
            .Where(policy => System.Text.Json.JsonSerializer.Serialize(policy.Value).Contains("StreamArn"))
            .Select(policy => policy.Key)
            .ToList();

        Assert.Equal(2, readers.Count);
        Assert.Contains(readers, key => key.Contains("dispatchplacedorders", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(readers, key => key.Contains("reviewrouting", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Synthesize_Always_RetriesOnlyTheRecordThatFailed()
    {
        var template = StackTemplate.SynthesizeWith();

        template.HasResourceProperties("AWS::Lambda::EventSourceMapping", Match.ObjectLike(
            new Dictionary<string, object>
            {
                ["FunctionResponseTypes"] = new[] { "ReportBatchItemFailures" },
                ["StartingPosition"] = "TRIM_HORIZON"
            }));
    }

    [Fact]
    public void Synthesize_Always_FeedsTheWorkerFromTheWorkQueue()
    {
        var template = StackTemplate.SynthesizeWith();

        template.HasResourceProperties("AWS::Lambda::EventSourceMapping", Match.ObjectLike(
            new Dictionary<string, object>
            {
                ["FunctionResponseTypes"] = new[] { "ReportBatchItemFailures" },
                ["BatchSize"] = 10,
                ["EventSourceArn"] = Match.ObjectLike(new Dictionary<string, object>
                {
                    ["Fn::GetAtt"] = Match.ArrayWith(new object[] { "Arn" })
                })
            }));
    }

    [Fact]
    public void Synthesize_Always_TellsOnlyTheDispatcherWhereTheWorkQueueIs()
    {
        var template = StackTemplate.SynthesizeWith();

        var functions = template.FindResources("AWS::Lambda::Function").Values
            .Select(function => System.Text.Json.JsonSerializer.Serialize(function))
            .ToList();

        Assert.Single(functions, function => function.Contains("WORK_QUEUE_URL"));
        Assert.Single(functions, function =>
            function.Contains("WORK_QUEUE_URL") && function.Contains("DispatchPlacedOrders"));
    }

    [Fact]
    public void Synthesize_Always_LetsTheWorkerReadAnOrderAndSettleItAndNothingElse()
    {
        var template = StackTemplate.SynthesizeWith();

        var worker = template.FindResources("AWS::IAM::Policy").Values
            .Select(policy => System.Text.Json.JsonSerializer.Serialize(policy))
            .Single(policy => policy.Contains("dynamodb:GetItem") && policy.Contains("sqs:ReceiveMessage"));

        Assert.Contains("dynamodb:UpdateItem", worker);
        Assert.DoesNotContain("dynamodb:Scan", worker);
        Assert.DoesNotContain("dynamodb:PutItem", worker);
        Assert.DoesNotContain("dynamodb:DeleteItem", worker);
    }

    [Fact]
    public void Synthesize_Always_FeedsTheSettlerFromTheParkingAreaAndNothingElse()
    {
        var template = StackTemplate.SynthesizeWith();

        var settler = template.FindResources("AWS::IAM::Policy").Values
            .Select(policy => System.Text.Json.JsonSerializer.Serialize(policy))
            .Single(policy => policy.Contains("work-parking") || policy.Contains("workparking"));

        Assert.Contains("sqs:ReceiveMessage", settler);
        Assert.Contains("dynamodb:UpdateItem", settler);
        Assert.DoesNotContain("dynamodb:GetItem", settler);
        Assert.DoesNotContain("dynamodb:Scan", settler);
        Assert.DoesNotContain("dynamodb:PutItem", settler);
    }

    [Fact]
    public void Synthesize_Always_GivesEveryQueueItsOwnConsumer()
    {
        var template = StackTemplate.SynthesizeWith();

        template.ResourceCountIs("AWS::Lambda::EventSourceMapping", 4);
    }

    // The parking area covers the queue lane only. Without this the first leg of every order's
    // journey drops a spent record and leaves the order pending for good.
    [Fact]
    public void Synthesize_Always_KeepsTheStreamRecordsTheDispatcherGivesUpOn()
    {
        var template = StackTemplate.SynthesizeWith();

        var streamMapping = template.FindResources("AWS::Lambda::EventSourceMapping").Values
            .Select(mapping => System.Text.Json.JsonSerializer.Serialize(mapping))
            .Single(mapping => mapping.Contains("StreamArn"));

        Assert.Contains("DestinationConfig", streamMapping);
        Assert.Contains("OnFailure", streamMapping);
        Assert.Contains(StreamParkingArea(template), streamMapping);
    }

    [Fact]
    public void Synthesize_Always_GivesTheReviewerNoWayToWriteToAnOrder()
    {
        var template = StackTemplate.SynthesizeWith();

        var reviewer = PolicyNamed(template, "revieworders");

        Assert.Contains("sqs:ReceiveMessage", reviewer);
        Assert.DoesNotContain("dynamodb:", reviewer);
    }

    [Fact]
    public void Synthesize_Always_SplitsSendingWorkFromConsumingIt()
    {
        var template = StackTemplate.SynthesizeWith();

        var policies = template.FindResources("AWS::IAM::Policy").Values
            .Select(policy => System.Text.Json.JsonSerializer.Serialize(policy))
            .ToList();

        Assert.DoesNotContain(policies, policy =>
            policy.Contains("sqs:SendMessage") && policy.Contains("sqs:ReceiveMessage"));
    }

    // Both references point at a logical id, and CDK strips the hyphens out of the queue name to
    // build it, so the readable name never appears in either resource.
    private static string StreamParkingArea(Template template) =>
        template.FindResources("AWS::SQS::Queue")
            .Single(queue => System.Text.Json.JsonSerializer.Serialize(queue.Value)
                .Contains("orderpipeline-dev-demo-stream-parking"))
            .Key;

    private static string PolicyNamed(Template template, string marker) =>
        System.Text.Json.JsonSerializer.Serialize(
            template.FindResources("AWS::IAM::Policy")
                .Single(policy => policy.Key.Contains(marker, StringComparison.OrdinalIgnoreCase))
                .Value);
}
