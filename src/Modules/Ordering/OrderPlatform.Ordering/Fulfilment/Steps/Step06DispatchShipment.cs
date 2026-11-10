using OrderPlatform.Integrations.Shipping.Contracts;
using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.Ordering.Fulfilment.Steps;

/// <summary>
/// Hands the parcel to the carrier: the order is Shipped. Undo: asks the carrier to bring the parcel back,
/// for a cancel before the delivery. The stock stays reserved until the delivery, so it can still be released.
/// </summary>
public sealed class Step06DispatchShipment(IShippingGateway shipping) : IPipelineStep<OrderFulfilmentContext>
{
    public const string Key = "DispatchShipment";

    public bool ShouldRun(OrderFulfilmentContext context) => context.Lines.Any(line => line.IsPhysical);

    public async Task<StepResult> ExecuteAsync(OrderFulfilmentContext context, CancellationToken cancellationToken)
    {
        var shipmentId = context.ShipmentId ?? throw new InvalidOperationException($"Order {context.OrderId} has no shipment to dispatch.");

        var dispatched = await shipping.DispatchAsync(
            new DispatchShipmentRequest(GatewayStepResults.IdempotencyKey(context.OrderId, Key, "Execute"), shipmentId), cancellationToken);
        return dispatched.ToStepResult();
    }

    /// <summary>The shipment itself is cancelled afterwards by the undo of PrepareShipment.</summary>
    public async Task<StepResult> CompensateAsync(OrderFulfilmentContext context, CancellationToken cancellationToken)
    {
        if (context.ShipmentId is not { } shipmentId)
        {
            return StepResult.Success();
        }

        var recalled = await shipping.RecallAsync(
            new RecallShipmentRequest(GatewayStepResults.IdempotencyKey(context.OrderId, Key, "Compensate"), shipmentId), cancellationToken);
        return recalled.ToStepResult();
    }
}
