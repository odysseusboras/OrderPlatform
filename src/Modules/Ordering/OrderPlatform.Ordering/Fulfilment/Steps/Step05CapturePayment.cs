using OrderPlatform.Integrations.Payments.Contracts;
using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.Ordering.Fulfilment.Steps;

/// <summary>
/// Takes the authorized money: the order is Paid. Undo: refunds the captured amount, for a cancel or a later failure
/// before the delivery. The authorization was used up by the capture, so nothing is left to void afterwards.
/// </summary>
public sealed class Step05CapturePayment(IPaymentGateway payments) : IPipelineStep<OrderFulfilmentContext>
{
    public const string Key = "CapturePayment";

    public bool ShouldRun(OrderFulfilmentContext context) => context.TotalToPay().Amount > 0;

    public async Task<StepResult> ExecuteAsync(OrderFulfilmentContext context, CancellationToken cancellationToken)
    {
        var authorizationId = context.AuthorizationId
            ?? throw new InvalidOperationException($"Order {context.OrderId} has no payment authorization to capture.");

        var result = await payments.CaptureAsync(
            new CapturePaymentRequest(GatewayStepResults.IdempotencyKey(context.OrderId, Key, "Execute"), authorizationId, context.TotalToPay()),
            cancellationToken);
        if (result.IsSuccess)
        {
            context.CaptureId = result.Value!.CaptureId;
        }

        return result.ToStepResult();
    }

    public async Task<StepResult> CompensateAsync(OrderFulfilmentContext context, CancellationToken cancellationToken)
    {
        if (context.CaptureId is not { } captureId)
        {
            return StepResult.Success();
        }

        var result = await payments.RefundAsync(
            new RefundPaymentRequest(GatewayStepResults.IdempotencyKey(context.OrderId, Key, "Compensate"), captureId, context.TotalToPay()),
            cancellationToken);
        if (result.IsSuccess)
        {
            context.CaptureId = null;
            context.AuthorizationId = null;
        }

        return result.ToStepResult();
    }
}
