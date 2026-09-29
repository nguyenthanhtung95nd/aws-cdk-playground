using System.Collections;
using OrderPipeline.Contracts;

namespace OrderPipeline.Functions.Observability;

public readonly struct LogScope(string field, string value)
    : IReadOnlyList<KeyValuePair<string, object>>
{
    // On the synchronous leg the id is the gateway's request id, because that is what the caller
    // is handed back. Everywhere after that it is the order id, which is the only thing that
    // survives the change stream: nothing carries a request id across that boundary. The two legs
    // meet on the line that records an accepted order, which names both.
    public static LogScope Correlation(string id) => new(LogFields.CorrelationId, id);

    // A batch shares one invocation, so this says which run a line came from without lumping every
    // order in that run under a single identity.
    public static LogScope Invocation(string awsRequestId) => new(LogFields.InvocationId, awsRequestId);

    public int Count => 1;

    public KeyValuePair<string, object> this[int index] => index == 0
        ? new KeyValuePair<string, object>(field, value)
        : throw new ArgumentOutOfRangeException(nameof(index));

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        yield return this[0];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => value;
}
