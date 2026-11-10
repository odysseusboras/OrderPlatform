using OrderPlatform.BuildingBlocks.Integration;

namespace OrderPlatform.Integrations.Shipping.Contracts;

/// <summary>The platform's view of the carrier.</summary>
public interface IShippingGateway
{
    Task<GatewayResult<ShipmentBooking>> BookAsync(BookShipmentRequest request, CancellationToken cancellationToken);

    Task<GatewayResult> CancelAsync(CancelShipmentRequest request, CancellationToken cancellationToken);

    Task<GatewayResult> DispatchAsync(DispatchShipmentRequest request, CancellationToken cancellationToken);

    /// <summary>Asks the carrier to bring back a parcel that is on its way and not delivered yet.</summary>
    Task<GatewayResult> RecallAsync(RecallShipmentRequest request, CancellationToken cancellationToken);
}
