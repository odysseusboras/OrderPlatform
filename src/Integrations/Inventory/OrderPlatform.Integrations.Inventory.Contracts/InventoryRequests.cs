namespace OrderPlatform.Integrations.Inventory.Contracts;

public sealed record ReserveStockRequest(string IdempotencyKey, Guid OrderId, IReadOnlyList<StockLine> Lines);

public sealed record StockLine(string Sku, int Quantity);

public sealed record StockReservation(string ReservationId, DateTimeOffset ExpiresAt);

public sealed record ReleaseStockRequest(string IdempotencyKey, string ReservationId);

public sealed record ConsumeStockRequest(string IdempotencyKey, string ReservationId);
