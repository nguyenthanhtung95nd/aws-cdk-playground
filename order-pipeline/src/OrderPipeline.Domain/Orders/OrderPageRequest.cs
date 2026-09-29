using OrderPipeline.Domain.Common;

namespace OrderPipeline.Domain.Orders;

public readonly record struct OrderPageRequest
{
    public const int DefaultSize = 25;
    public const int MaxSize = 100;

    private OrderPageRequest(int size, string? resumeFrom)
    {
        Size = size;
        ResumeFrom = resumeFrom;
    }

    public int Size { get; }

    // Whatever the store handed back last time. The domain says only that it came from the store
    // and goes back to it unread; what it means is the store's business alone.
    public string? ResumeFrom { get; }

    public static OrderPageRequest First => new(DefaultSize, null);

    public static Result<OrderPageRequest> For(int? size, string? resumeFrom)
    {
        if (size is { } requested && (requested < 1 || requested > MaxSize))
        {
            return Result<OrderPageRequest>.Failure($"A page must hold between 1 and {MaxSize} orders.");
        }

        return Result<OrderPageRequest>.Success(new OrderPageRequest(
            size ?? DefaultSize,
            string.IsNullOrWhiteSpace(resumeFrom) ? null : resumeFrom));
    }
}
