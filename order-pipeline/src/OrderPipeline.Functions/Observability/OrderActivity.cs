using System.Diagnostics;
using Amazon.Lambda.SQSEvents;
using OrderPipeline.Contracts;
using OrderPipeline.ServiceDefaults;

namespace OrderPipeline.Functions.Observability;

/// <summary>
/// One span per order inside an invocation, tagged so a trace can be found by order id.
/// </summary>
internal static class OrderActivity
{
    private const string Name = "order";

    public static Activity? Begin(string orderId) =>
        Tag(LambdaTelemetry.Source.StartActivity(Name), orderId);

    // The dispatcher stamped the message with its trace, so this span joins that trace.
    public static Activity? Begin(string orderId, SQSEvent.SQSMessage message) =>
        Tag(LambdaTelemetry.Source.StartActivity(Name, ActivityKind.Consumer, ParentOf(message)), orderId);

    private static Activity? Tag(Activity? activity, string orderId) =>
        activity?.SetTag(TraceTags.OrderId, orderId);

    private static ActivityContext ParentOf(SQSEvent.SQSMessage message) =>
        message.MessageAttributes is { } attributes
        && attributes.TryGetValue(MessageAttributes.TraceParent, out var attribute)
        && ActivityContext.TryParse(attribute.StringValue, null, out var parent)
            ? parent
            : default;
}
