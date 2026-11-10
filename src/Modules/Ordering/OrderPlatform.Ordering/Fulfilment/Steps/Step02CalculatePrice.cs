using OrderPlatform.Pricing.Contracts;
using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.Ordering.Fulfilment.Steps;

/// <summary>
/// Calculates the order total with VAT and shipping and stores it in the context. The order gets the price
/// through <see cref="OrderProgressObserver"/>. Undo: nothing is held anywhere; the price stays on the order for audit.
/// </summary>
public sealed class Step02CalculatePrice(IPricingService pricing) : IPipelineStep<OrderFulfilmentContext>
{
    public const string Key = "CalculatePrice";

    /// <summary>Every order needs a price.</summary>
    public bool ShouldRun(OrderFulfilmentContext context) => true;

    /// <summary>
    /// A product that is unknown, inactive or without a price is a fatal error: the order is rejected.
    /// Pricing is local, so there is no expected transient failure; an exception (database down) is retried by the engine.
    /// </summary>
    public async Task<StepResult> ExecuteAsync(OrderFulfilmentContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var request = new PriceRequest(context.Lines.Select(line => new PriceRequestLine(line.ProductId, line.Quantity)).ToList());

        var result = await pricing.CalculateAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return StepResult.Fatal($"{result.Error!.Code}: {result.Error.Message}");
        }

        var quote = result.Value!;
        context.Price = new PriceSnapshot(quote.Subtotal, quote.Tax, quote.ShippingCharge, quote.Total);
        return StepResult.Success();
    }

    public Task<StepResult> CompensateAsync(OrderFulfilmentContext context, CancellationToken cancellationToken) =>
        Task.FromResult(StepResult.Success());
}
