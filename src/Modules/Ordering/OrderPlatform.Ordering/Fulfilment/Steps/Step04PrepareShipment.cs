using OrderPlatform.Integrations.Shipping.Contracts;
using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.Ordering.Fulfilment.Steps;

/// <summary>
/// Books the carrier and creates the label. It runs before the capture on purpose: a failed booking is cheap to undo.
/// Undo: cancels the booking.
/// </summary>
public sealed class Step04PrepareShipment(IShippingGateway shipping) : IPipelineStep<OrderFulfilmentContext>
{
    public const string Key = "PrepareShipment";

    /// <summary>Digital products are not shipped.</summary>
    public bool ShouldRun(OrderFulfilmentContext context) => context.Lines.Any(line => line.IsPhysical);

    public async Task<StepResult> ExecuteAsync(OrderFulfilmentContext context, CancellationToken cancellationToken)
    {
        var request = new BookShipmentRequest(
            GatewayStepResults.IdempotencyKey(context.OrderId, Key, "Execute"),
            context.OrderId,
            context.ShippingAddress,
            context.Lines.Where(line => line.IsPhysical).Select(line => new ShipmentLine(line.Sku, line.Quantity)).ToList());

        var result = await shipping.BookAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            context.ShipmentId = result.Value!.ShipmentId;
            context.TrackingNumber = result.Value.TrackingNumber;
        }

        return result.ToStepResult();
    }

    public async Task<StepResult> CompensateAsync(OrderFulfilmentContext context, CancellationToken cancellationToken)
    {
        if (context.ShipmentId is not { } shipmentId)
        {
            return StepResult.Success();
        }

        var result = await shipping.CancelAsync(
            new CancelShipmentRequest(GatewayStepResults.IdempotencyKey(context.OrderId, Key, "Compensate"), shipmentId), cancellationToken);
        if (result.IsSuccess)
        {
            context.ShipmentId = null;
            context.TrackingNumber = null;
        }

        return result.ToStepResult();
    }
}
