using System.Collections.Concurrent;
using OrderPlatform.BuildingBlocks.Integration;
using OrderPlatform.Integrations.Inventory.Contracts;

namespace OrderPlatform.FakeExternalSystems.Inventory;

/// <summary>
/// In-memory stand-in for the inventory system. Behaves like the real one is expected to: the same idempotency key
/// returns the same reservation, and releasing twice is harmless. Development and tests only.
/// </summary>
internal sealed class FakeInventoryGateway(FakeExternalSystemsOptions options, TimeProvider timeProvider) : IInventoryGateway
{
    private static readonly TimeSpan ReservationLifetime = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<string, StockReservation> _reservationsByKey = new();
    private readonly ConcurrentDictionary<string, StockReservation> _activeReservations = new();

    public Task<GatewayResult<StockReservation>> ReserveAsync(ReserveStockRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (_reservationsByKey.TryGetValue(request.IdempotencyKey, out var existing))
        {
            return Task.FromResult(GatewayResult<StockReservation>.Success(existing));
        }

        var outOfStock = request.Lines.Select(line => line.Sku).Where(Configured(options.Inventory.OutOfStockSkus)).ToList();
        if (outOfStock.Count > 0)
        {
            return Task.FromResult(GatewayResult<StockReservation>.Permanent($"Out of stock: {string.Join(", ", outOfStock)}."));
        }

        if (request.Lines.Select(line => line.Sku).Any(Configured(options.Inventory.UnavailableSkus)))
        {
            return Task.FromResult(GatewayResult<StockReservation>.Transient("The inventory system is unavailable."));
        }

        var reservation = _reservationsByKey.GetOrAdd(
            request.IdempotencyKey,
            _ => new StockReservation($"res-{Guid.CreateVersion7():N}", timeProvider.GetUtcNow() + ReservationLifetime));
        _activeReservations[reservation.ReservationId] = reservation;

        return Task.FromResult(GatewayResult<StockReservation>.Success(reservation));
    }

    public Task<GatewayResult> ReleaseAsync(ReleaseStockRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        _activeReservations.TryRemove(request.ReservationId, out _);

        return Task.FromResult(GatewayResult.Success());
    }

    public Task<GatewayResult> ConsumeAsync(ConsumeStockRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_activeReservations.TryRemove(request.ReservationId, out _)
            ? GatewayResult.Success()
            : GatewayResult.Permanent($"Reservation {request.ReservationId} does not exist or was released."));
    }

    /// <summary>True while the reservation is held. For tests and demos.</summary>
    internal bool IsReserved(string reservationId) => _activeReservations.ContainsKey(reservationId);

    private static Func<string, bool> Configured(IList<string> skus) =>
        sku => skus.Contains(sku, StringComparer.OrdinalIgnoreCase);
}
