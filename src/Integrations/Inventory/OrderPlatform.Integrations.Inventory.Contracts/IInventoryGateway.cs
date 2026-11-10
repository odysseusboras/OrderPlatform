using OrderPlatform.BuildingBlocks.Integration;

namespace OrderPlatform.Integrations.Inventory.Contracts;

/// <summary>The platform's view of the external inventory system.</summary>
public interface IInventoryGateway
{
    Task<GatewayResult<StockReservation>> ReserveAsync(ReserveStockRequest request, CancellationToken cancellationToken);

    Task<GatewayResult> ReleaseAsync(ReleaseStockRequest request, CancellationToken cancellationToken);

    Task<GatewayResult> ConsumeAsync(ConsumeStockRequest request, CancellationToken cancellationToken);
}
