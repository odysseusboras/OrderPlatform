using OrderPlatform.Integrations.Inventory.Contracts;
using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.Ordering.Fulfilment.Steps;

/// <summary>
/// Reserves the ordered items in the inventory system. This is the availability check: an order with an item
/// that is out of stock is rejected before anything else happens. Undo: releases the reservation.
/// </summary>
public sealed class Step01ReserveStock(IInventoryGateway inventory) : IPipelineStep<OrderFulfilmentContext>
{
    public const string Key = "ReserveStock";

    /// <summary>Digital products need no stock.</summary>
    public bool ShouldRun(OrderFulfilmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Lines.Any(line => line.IsPhysical);
    }

    /// <summary>Out of stock is a fatal error (the order is rejected); an unreachable inventory is retried.</summary>
    public async Task<StepResult> ExecuteAsync(OrderFulfilmentContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var request = new ReserveStockRequest(
            GatewayStepResults.IdempotencyKey(context.OrderId, Key, "Execute"),
            context.OrderId,
            context.Lines.Where(line => line.IsPhysical).Select(line => new StockLine(line.Sku, line.Quantity)).ToList());

        var result = await inventory.ReserveAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            context.ReservationId = result.Value!.ReservationId;
        }

        return result.ToStepResult();
    }

    /// <summary>Safe to call twice: with nothing reserved there is nothing to release.</summary>
    public async Task<StepResult> CompensateAsync(OrderFulfilmentContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ReservationId is not { } reservationId)
        {
            return StepResult.Success();
        }

        var request = new ReleaseStockRequest(GatewayStepResults.IdempotencyKey(context.OrderId, Key, "Compensate"), reservationId);
        var result = await inventory.ReleaseAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            context.ReservationId = null;
        }

        return result.ToStepResult();
    }
}
