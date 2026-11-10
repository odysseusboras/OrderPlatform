using OrderPlatform.BuildingBlocks.Integration;
using OrderPlatform.Integrations.Inventory.Contracts;

namespace OrderPlatform.Integrations.Inventory;

/// <summary>
/// Placeholder for the client of the real inventory system: it answers every call as if the inventory accepted it,
/// so the order flow runs end to end. It checks nothing and reserves nothing.
/// Replace it with the real HTTP client before any real order is taken (milestone "real inventory integration").
/// </summary>
internal sealed class InventoryGateway : IInventoryGateway
{
    // ponytail: always succeeds; the real client maps HTTP answers to Success / Transient / Permanent.
    public Task<GatewayResult<StockReservation>> ReserveAsync(ReserveStockRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // The same key gives the same reservation id, as the real system must.
        var reservation = new StockReservation($"placeholder-{request.IdempotencyKey}", DateTimeOffset.MaxValue);
        return Task.FromResult(GatewayResult<StockReservation>.Success(reservation));
    }

    public Task<GatewayResult> ReleaseAsync(ReleaseStockRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GatewayResult.Success());
    }

    public Task<GatewayResult> ConsumeAsync(ConsumeStockRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GatewayResult.Success());
    }
}
