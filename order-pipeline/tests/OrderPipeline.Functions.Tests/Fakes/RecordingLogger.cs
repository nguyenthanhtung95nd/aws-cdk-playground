using Microsoft.Extensions.Logging;

namespace OrderPipeline.Functions.Tests.Fakes;

public sealed record LoggedLine(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Scope);

public sealed class RecordingLogger<TCategory> : ILogger<TCategory>
{
    private readonly List<LoggedLine> _lines = [];
    private readonly Dictionary<string, object?> _activeScope = [];

    public IReadOnlyList<LoggedLine> Lines => _lines;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        if (state is not IEnumerable<KeyValuePair<string, object>> fields)
        {
            return new ScopeHandle(this, []);
        }

        var keys = fields.Select(field => field.Key).ToArray();
        foreach (var field in fields)
        {
            _activeScope[field.Key] = field.Value;
        }

        return new ScopeHandle(this, keys);
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _lines.Add(new LoggedLine(
            logLevel,
            formatter(state, exception),
            new Dictionary<string, object?>(_activeScope)));
    }

    private sealed class ScopeHandle(RecordingLogger<TCategory> owner, IReadOnlyList<string> keys) : IDisposable
    {
        public void Dispose()
        {
            foreach (var key in keys)
            {
                owner._activeScope.Remove(key);
            }
        }
    }
}
