using OrderPlatform.BuildingBlocks.Integration;
using OrderPlatform.Integrations.Shipping.Contracts;

namespace OrderPlatform.FakeExternalSystems.Shipping;

/// <summary>In-memory stand-in for the carrier: it accepts every call. Development and tests only.</summary>
internal sealed class FakeShippingGateway : IShippingGateway
{
    // ponytail: always succeeds; the same key gives the same booking, as the real carrier must.
    public Task<GatewayResult<ShipmentBooking>> BookAsync(BookShipmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var booking = new ShipmentBooking($"fake-shipment-{request.IdempotencyKey}", $"TRK-{request.OrderId:N}");
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
