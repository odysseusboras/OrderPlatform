using System.ComponentModel.DataAnnotations;
using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.Ordering.Orders;
using OrderPlatform.Ordering.Placing;

namespace OrderPlatform.Api.Models;

public sealed record CreateOrderRequest(
    [Required, MinLength(1), MaxLength(CreateOrderRequest.MaxLines)] IReadOnlyList<OrderLineRequest> Lines,
    [Required] AddressModel ShippingAddress,
    [Required, StringLength(200, MinimumLength = 1)] string PaymentToken,
    bool HoldBeforePayment = false)
{
    public const int MaxLines = 50;

    public PlaceOrderCommand ToCommand(Guid customerId, TimeSpan? waitForPrice) =>
        new(
            customerId,
            Lines.Select(line => new RequestedLine(line.ProductId, line.Quantity)).ToList(),
            ShippingAddress.ToShippingAddress(),
            PaymentToken,
            waitForPrice,
            HoldBeforePayment);
}

public sealed record OrderLineRequest(
    [Required] Guid ProductId,
    [Range(1, OrderLineRequest.MaxQuantity)] int Quantity)
{
    public const int MaxQuantity = 1000;
}

public sealed record AddressModel(
    [Required, StringLength(200)] string Recipient,
    [Required, StringLength(200)] string Street,
    [Required, StringLength(100)] string City,
    [Required, StringLength(20)] string PostalCode,
    [Required, StringLength(2, MinimumLength = 2)] string CountryCode)
{
    public ShippingAddress ToShippingAddress() => new(Recipient, Street, City, PostalCode, CountryCode.ToUpperInvariant());

    public static AddressModel From(ShippingAddress address) =>
        new(address.Recipient, address.Street, address.City, address.PostalCode, address.CountryCode);
}

/// <summary>An order with its items, price (once calculated) and every status change.</summary>
public sealed record OrderResponse(
    Guid OrderId,
    OrderStatus Status,
    bool CanCancel,
    Currency Currency,
    OrderPriceResponse? Price,
    IReadOnlyList<OrderItemResponse> Items,
    AddressModel ShippingAddress,
    IReadOnlyList<OrderStatusChangeResponse> StatusHistory,
    DateTimeOffset CreatedAt)
{
    public static OrderResponse From(Order order) =>
        new(
            order.Id,
            order.Status,
            order.CanCancel,
            order.Currency,
            OrderPriceResponse.From(order),
            order.Items.Select(item => new OrderItemResponse(item.ProductId, item.Sku, item.Quantity, item.IsPhysical)).ToList(),
            AddressModel.From(order.ShippingAddress),
            order.StatusHistory
                .OrderBy(change => change.Sequence)
                .Select(change => new OrderStatusChangeResponse(change.Sequence, change.FromStatus, change.ToStatus, change.Reason, change.CreatedAt))
                .ToList(),
            order.CreatedAt);
}

public sealed record OrderPriceResponse(decimal Subtotal, decimal Tax, decimal ShippingCharge, decimal Total)
{
    public static OrderPriceResponse? From(Order order) =>
        order is { SubtotalAmount: { } subtotal, TaxAmount: { } tax, ShippingChargeAmount: { } shipping, TotalAmount: { } total }
            ? new OrderPriceResponse(subtotal, tax, shipping, total)
            : null;
}

public sealed record OrderItemResponse(Guid ProductId, string Sku, int Quantity, bool IsPhysical);

public sealed record OrderStatusChangeResponse(int Sequence, OrderStatus? From, OrderStatus To, string? Reason, DateTimeOffset At);

public sealed record OrderSummaryResponse(Guid OrderId, OrderStatus Status, Currency Currency, decimal? Total, int ItemCount, DateTimeOffset CreatedAt)
{
    public static OrderSummaryResponse From(OrderSummary order) =>
        new(order.OrderId, order.Status, order.Currency, order.TotalAmount, order.ItemCount, order.CreatedAt);
}
