using OrderPlatform.BuildingBlocks.Integration;
using OrderPlatform.Integrations.Shipping.Contracts;

namespace OrderPlatform.Integrations.Shipping;

/// <summary>Placeholder for the client of the real carrier: it accepts every call and ships nothing. Replace it with the real client before any real order is taken.</summary>
internal sealed class ShippingGateway : IShippingGateway
{
    // ponytail: always succeeds; the same key gives the same booking, as the real carrier must.
    public Task<GatewayResult<ShipmentBooking>> BookAsync(BookShipmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var booking = new ShipmentBooking($"placeholder-shipment-{request.IdempotencyKey}", $"TRK-{request.OrderId:N}");
        return Task.FromResult(GatewayResult<ShipmentBooking>.Success(booking));
    }

    public Task<GatewayResult> CancelAsync(CancelShipmentRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GatewayResult.Success());
    }

    public Task<GatewayResult> DispatchAsync(DispatchShipmentRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GatewayResult.Success());
    }

    public Task<GatewayResult> RecallAsync(RecallShipmentRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GatewayResult.Success());
    }
}
