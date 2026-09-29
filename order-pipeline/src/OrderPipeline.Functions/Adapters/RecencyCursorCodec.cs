using System.Text.Json;
using Amazon.DynamoDBv2.Model;
using OrderPipeline.Contracts;
using OrderPipeline.Domain.Common;

namespace OrderPipeline.Functions.Adapters;

// A query on the recency index stops at a key made of three attributes: the table's own key plus
// the index's two. All three have to reach the caller and come back, and the caller is a browser,
// so they travel as one opaque string rather than three query parameters nobody should be composing.
internal static class RecencyCursorCodec
{
    private static readonly string[] Keys =
    [
        OrderAttributes.OrderId,
        OrderAttributes.RecencyBucket,
        OrderAttributes.RecencyCursor
    ];

    public static string? Encode(Dictionary<string, AttributeValue>? lastEvaluatedKey)
    {
        if (lastEvaluatedKey is not { Count: > 0 })
        {
            return null;
        }

        var parts = new Dictionary<string, string>(Keys.Length);

        foreach (var key in Keys)
        {
            if (!lastEvaluatedKey.TryGetValue(key, out var value) || value.S is not { Length: > 0 })
            {
                return null;
            }

            parts[key] = value.S;
        }

        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(parts));
    }

    // The cursor arrives on the query string, so it is a stranger's text until it proves otherwise.
    // Anything unreadable is refused rather than allowed to escape as an exception.
    public static Result<Dictionary<string, AttributeValue>?> Decode(string? cursor)
    {
        if (cursor is null)
        {
            return Result<Dictionary<string, AttributeValue>?>.Success(null);
        }

        try
        {
            var parts = JsonSerializer.Deserialize<Dictionary<string, string>>(
                Convert.FromBase64String(cursor));

            if (parts is null || Keys.Any(key => !parts.TryGetValue(key, out var part) || part.Length == 0))
            {
                return Unreadable;
            }

            return Result<Dictionary<string, AttributeValue>?>.Success(
                Keys.ToDictionary(key => key, key => new AttributeValue { S = parts[key] }));
        }
        catch (Exception cause) when (cause is FormatException or JsonException)
        {
            return Unreadable;
        }
    }

    private static Result<Dictionary<string, AttributeValue>?> Unreadable =>
        Result<Dictionary<string, AttributeValue>?>.Failure("The cursor is not one this API handed out.");
}
