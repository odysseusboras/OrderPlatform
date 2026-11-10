namespace OrderPlatform.Integrations.Shipping.Contracts;

public sealed record BookShipmentRequest(
    string IdempotencyKey,
    Guid OrderId,
    ShippingAddress Address,
    IReadOnlyList<ShipmentLine> Lines);

public sealed record ShippingAddress(string Recipient, string Street, string City, string PostalCode, string CountryCode);

public sealed record ShipmentLine(string Sku, int Quantity);

public sealed record ShipmentBooking(string ShipmentId, string TrackingNumber);

public sealed record CancelShipmentRequest(string IdempotencyKey, string ShipmentId);

public sealed record DispatchShipmentRequest(string IdempotencyKey, string ShipmentId);

public sealed record RecallShipmentRequest(string IdempotencyKey, string ShipmentId);
