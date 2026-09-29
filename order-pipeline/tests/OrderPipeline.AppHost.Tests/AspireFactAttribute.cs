namespace OrderPipeline.AppHost.Tests;

/// <summary>
/// A fact that starts the whole local topology, so it runs only when asked for:
/// set <c>ORDER_PIPELINE_ASPIRE_TESTS=1</c> (see <c>npm run verify:aspire</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AspireFactAttribute : FactAttribute
{
    public const string Switch = "ORDER_PIPELINE_ASPIRE_TESTS";

    public AspireFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(Switch) != "1")
        {
            Skip = $"Needs Docker and the full host. Set {Switch}=1 to run.";
        }
    }
}
