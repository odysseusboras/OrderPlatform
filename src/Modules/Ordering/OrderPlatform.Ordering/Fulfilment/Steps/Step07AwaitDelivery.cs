using OrderPlatform.Integrations.Inventory.Contracts;
using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.Ordering.Fulfilment.Steps;

/// <summary>
/// Waits for the delivery confirmation. The run stops here until the delivery is confirmed (which sets
/// <see cref="OrderFulfilmentContext.DeliveredAt"/> and wakes the run); then the stock reservation becomes a sale.
/// The pivot: until the delivery a cancel or a failure undoes every earlier step; a delivered order is final,
/// and getting it back is a return, not a cancel. The timeout in the pipeline definition escalates a lost parcel.
/// </summary>
public sealed class Step07AwaitDelivery(IInventoryGateway inventory) : IPipelineStep<OrderFulfilmentContext>
{
    public const string Key = "AwaitDelivery";

    /// <summary>A digital order is complete once it is paid.</summary>
    public bool ShouldRun(OrderFulfilmentContext context) => context.Lines.Any(line => line.IsPhysical);

    public async Task<StepResult> ExecuteAsync(OrderFulfilmentContext context, CancellationToken cancellationToken)
    {
        if (context.DeliveredAt is null)
        {
            return StepResult.Waiting();
        }

        var reservationId = context.ReservationId ?? throw new InvalidOperationException($"Order {context.OrderId} has no stock reservation to consume.");
        var consumed = await inventory.ConsumeAsync(
            new ConsumeStockRequest(GatewayStepResults.IdempotencyKey(context.OrderId, "ConsumeStock", "Execute"), reservationId), cancellationToken);
        return consumed.ToStepResult();
    }

    public Task<StepResult> CompensateAsync(OrderFulfilmentContext context, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The pivot is never undone. A call here is an engine bug.");
}
