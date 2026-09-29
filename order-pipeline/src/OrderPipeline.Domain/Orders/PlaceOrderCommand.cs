using OrderPipeline.Domain.Common;
using System.Text.Json;

namespace OrderPipeline.Domain.Orders;

public sealed record PlaceOrderCommand(string CustomerName, string Product, decimal Amount, string Notes)
{
    public const int MaxCustomerNameLength = 200;
    public const int MaxProductLength = 200;
    public const int MaxNotesLength = 1000;

    public static Result<PlaceOrderCommand> Parse(string? body)
    {
        JsonElement root;

        try
        {
            using var document = JsonDocument.Parse(body ?? string.Empty);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return Result<PlaceOrderCommand>.Failure("Request body must be valid JSON.");
        }

        if (root.ValueKind is not JsonValueKind.Object)
        {
            return Result<PlaceOrderCommand>.Failure("Request body must be valid JSON.");
        }

        var customerName = ReadRequiredText(root, "customerName", MaxCustomerNameLength);
        if (!customerName.IsSuccess)
        {
            return Result<PlaceOrderCommand>.Failure(customerName.Error!);
        }

        var product = ReadRequiredText(root, "product", MaxProductLength);
        if (!product.IsSuccess)
        {
            return Result<PlaceOrderCommand>.Failure(product.Error!);
        }

        var amount = ReadAmount(root);
        if (!amount.IsSuccess)
        {
            return Result<PlaceOrderCommand>.Failure(amount.Error!);
        }

        var notes = ReadNotes(root);
        if (!notes.IsSuccess)
        {
            return Result<PlaceOrderCommand>.Failure(notes.Error!);
        }

        return Result<PlaceOrderCommand>.Success(
            new PlaceOrderCommand(customerName.Value!, product.Value!, amount.Value, notes.Value!));
    }

    private static Result<string> ReadRequiredText(JsonElement root, string field, int maxLength)
    {
        if (!root.TryGetProperty(field, out var element)
            || element.ValueKind is not JsonValueKind.String
            || string.IsNullOrWhiteSpace(element.GetString()))
        {
            return Result<string>.Failure($"{field} is required.");
        }

        var text = element.GetString()!.Trim();

        return text.Length > maxLength
            ? Result<string>.Failure($"{field} must be at most {maxLength} characters.")
            : Result<string>.Success(text);
    }

    private static Result<decimal> ReadAmount(JsonElement root)
    {
        if (!root.TryGetProperty("amount", out var element) || element.ValueKind is JsonValueKind.Null)
        {
            return Result<decimal>.Failure("amount is required.");
        }

        if (element.ValueKind is not JsonValueKind.Number || !element.TryGetDecimal(out var amount))
        {
            return Result<decimal>.Failure("amount must be a number.");
        }

        return amount <= 0
            ? Result<decimal>.Failure("amount must be greater than zero.")
            : Result<decimal>.Success(amount);
    }

    private static Result<string> ReadNotes(JsonElement root)
    {
        if (!root.TryGetProperty("notes", out var element) || element.ValueKind is not JsonValueKind.String)
        {
            return Result<string>.Success(string.Empty);
        }

        var notes = element.GetString()!.Trim();

        return notes.Length > MaxNotesLength
            ? Result<string>.Failure($"notes must be at most {MaxNotesLength} characters.")
            : Result<string>.Success(notes);
    }
}
