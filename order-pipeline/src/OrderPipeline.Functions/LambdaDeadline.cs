using Amazon.Lambda.Core;

namespace OrderPipeline.Functions;

public static class LambdaDeadline
{
    private static readonly TimeSpan ReportingBudget = TimeSpan.FromMilliseconds(500);

    // Stop our own work slightly before Lambda kills the invocation, so the failure still gets logged.
    public static CancellationTokenSource For(ILambdaContext context)
    {
        var budget = context.RemainingTime - ReportingBudget;
        return new CancellationTokenSource(budget > TimeSpan.Zero ? budget : TimeSpan.Zero);
    }
}
