using Amazon.CDK;

namespace OrderPipeline.Infrastructure;

internal static class FunctionDefaults
{
    public static readonly Duration Timeout = Duration.Seconds(30);

    // A message stays invisible for six invocations' worth of time, so a redelivery can only
    // happen after the attempt before it has certainly stopped running.
    public static readonly Duration WorkVisibility = Duration.Seconds(Timeout.ToSeconds() * 6);

    // Three different services count attempts here, and they are kept apart on purpose even while
    // they agree: how many times a queue hands the same message back, how many times the event
    // source replays a stream batch, and how many times a pipe replays a stream record. One shared
    // constant would make tuning any of them silently change the other two.
    public const int RetryBudget = 3;

    public const int StreamRetryBudget = 3;

    public const int PipeRetryBudget = 3;
}
