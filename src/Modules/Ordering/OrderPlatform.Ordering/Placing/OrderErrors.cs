using OrderPlatform.BuildingBlocks.Results;

namespace OrderPlatform.Ordering.Placing;

public static class OrderErrors
{
    public static readonly Error NotFound = new("ordering.order_not_found", "The order does not exist.");

    public static readonly Error DuplicateProduct = new("ordering.duplicate_product", "A product can appear only once; use the quantity instead.");

    public static readonly Error MixedCurrencies = new("ordering.mixed_currencies", "The products are sold in different currencies and cannot be in one order.");

    public static readonly Error CannotCancel = new(
        "ordering.cannot_cancel",
        "The order can no longer be cancelled: it is delivered, or it is already cancelled, rejected or failed.");

    public static readonly Error NotAwaitingDelivery = new(
        "ordering.not_awaiting_delivery",
        "The order is not waiting for a delivery confirmation: it is not shipped yet, or it is already delivered or stopped.");

    public static readonly Error NotOnHold = new(
        "ordering.not_on_hold",
        "The order is not on hold before payment: it was not placed with a hold, it was already released, or it has stopped.");

    public static Error ProductsNotAvailable(IEnumerable<Guid> productIds) =>
        new("ordering.products_not_available", $"These products are unknown, inactive or have no price: {string.Join(", ", productIds)}.");
}
