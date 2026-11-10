using OrderPlatform.Integrations.Payments.Contracts;
using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.Ordering.Fulfilment.Steps;

/// <summary>
/// Holds the order total on the customer's payment method. An order placed with "hold before payment" waits here first, until it is released. Once it succeeds the order is accepted (Confirmed):
/// the stock is reserved and the money is held. Undo: voids the authorization, which is free because nothing was captured.
/// </summary>
public sealed class Step03AuthorizePayment(IPaymentGateway payments) : IPipelineStep<OrderFulfilmentContext>
{
    public const string Key = "AuthorizePayment";

    /// <summary>A free order needs no payment.</summary>
    public bool ShouldRun(OrderFulfilmentContext context) => context.TotalToPay().Amount > 0;

    /// <summary>Only the provider's payment token is sent; card data never reaches the platform. A decline is fatal: the order is rejected.</summary>
    public async Task<StepResult> ExecuteAsync(OrderFulfilmentContext context, CancellationToken cancellationToken)
    {
        if (context.HoldBeforePayment)
        {
            // ponytail: no time limit, so a held order keeps its stock until it is released or cancelled.
            // Give the step a timeout in the next pipeline version if holds are opened to real customers.
            return StepResult.Waiting();
        }

        var request = new AuthorizePaymentRequest(
            GatewayStepResults.IdempotencyKey(context.OrderId, Key, "Execute"), context.OrderId, context.PaymentToken, context.TotalToPay());

        var result = await payments.AuthorizeAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            context.AuthorizationId = result.Value!.AuthorizationId;
        }

        return result.ToStepResult();
    }

    public async Task<StepResult> CompensateAsync(OrderFulfilmentContext context, CancellationToken cancellationToken)
    {
        if (context.AuthorizationId is not { } authorizationId)
        {
            return StepResult.Success();
        }

        var result = await payments.VoidAsync(
            new VoidPaymentRequest(GatewayStepResults.IdempotencyKey(context.OrderId, Key, "Compensate"), authorizationId), cancellationToken);
        if (result.IsSuccess)
        {
            context.AuthorizationId = null;
        }

        return result.ToStepResult();
    }
}
