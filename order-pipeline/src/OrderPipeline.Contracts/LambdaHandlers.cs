namespace OrderPipeline.Contracts;

public static class LambdaHandlers
{
    public const string Assembly = "OrderPipeline.Functions";

    public const string GetHealth = "GetHealth";
    public const string PlaceOrder = "PlaceOrder";
    public const string ListOrders = "ListOrders";
    public const string DispatchPlacedOrders = "DispatchPlacedOrders";
    public const string CompleteOrders = "CompleteOrders";
    public const string SettleFailures = "SettleFailures";
    public const string ReviewOrders = "ReviewOrders";

    public static readonly string[] All =
    [
        GetHealth, PlaceOrder, ListOrders, DispatchPlacedOrders, CompleteOrders, SettleFailures, ReviewOrders
    ];
}
