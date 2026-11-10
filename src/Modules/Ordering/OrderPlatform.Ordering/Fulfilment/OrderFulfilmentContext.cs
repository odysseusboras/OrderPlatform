using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.Ordering.Orders;
using ShippingAddress = OrderPlatform.Integrations.Shipping.Contracts.ShippingAddress;

namespace OrderPlatform.Ordering.Fulfilment;

/// <summary>
/// The state shared by the steps of the order fulfilment pipeline. Persisted with the run after every step.
/// Inputs are copied from the order when the run starts; results are filled in by the steps.
/// </summary>
public sealed class OrderFulfilmentContext
{
    public required Guid OrderId { get; init; }

    public required IReadOnlyList<FulfilmentLine> Lines { get; init; }

    public required string PaymentToken { get; init; }

    public required ShippingAddress ShippingAddress { get; init; }

    /// <summary>Set by CalculatePrice. The total is the amount later authorized and captured.</summary>
    public PriceSnapshot? Price { get; set; }

    public string? ReservationId { get; set; }

    public string? AuthorizationId { get; set; }

    public string? ShipmentId { get; set; }

    public string? TrackingNumber { get; set; }

    public string? CaptureId { get; set; }

    /// <summary>
    /// Asked for when the order was placed: the run stops before the payment is authorized, with the stock reserved
    /// and the price known, until the customer releases the order (which clears this) or cancels it.
    /// </summary>
    public bool HoldBeforePayment { get; set; }

    /// <summary>Set by the delivery trigger (carrier webhook or back-office) before AwaitDelivery runs again.</summary>
    public DateTimeOffset? DeliveredAt { get; set; }

    /// <summary>The amount to authorize and capture. A method, not a property, so it is not stored with the context.</summary>
    public Money TotalToPay() =>
        Price?.Total ?? throw new InvalidOperationException($"Order {OrderId} has no price yet; CalculatePrice must run before the payment steps.");

    public static OrderFulfilmentContext From(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        var address = order.ShippingAddress;

        return new OrderFulfilmentContext
        {
            OrderId = order.Id,
            Lines = order.Items.Select(item => new FulfilmentLine(item.ProductId, item.Sku, item.Quantity, item.IsPhysical)).ToList(),
            PaymentToken = order.PaymentToken,
            ShippingAddress = new ShippingAddress(address.Recipient, address.Street, address.City, address.PostalCode, address.CountryCode),
        };
    }
}

public sealed record FulfilmentLine(Guid ProductId, string Sku, int Quantity, bool IsPhysical);

/// <summary>The price calculated for the order: VAT per line, then the additional charges.</summary>
public sealed record PriceSnapshot(Money Subtotal, Money Tax, Money ShippingCharge, Money Total);
