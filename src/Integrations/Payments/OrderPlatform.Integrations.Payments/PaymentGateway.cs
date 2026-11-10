using OrderPlatform.BuildingBlocks.Integration;
using OrderPlatform.Integrations.Payments.Contracts;

namespace OrderPlatform.Integrations.Payments;

/// <summary>Placeholder for the client of the real payment provider: it accepts every call and moves no money. Replace it with the real client before any real order is taken.</summary>
internal sealed class PaymentGateway : IPaymentGateway
{
    // ponytail: always succeeds; the same key gives the same id, as the real provider must.
    public Task<GatewayResult<PaymentAuthorization>> AuthorizeAsync(AuthorizePaymentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GatewayResult<PaymentAuthorization>.Success(new PaymentAuthorization($"placeholder-auth-{request.IdempotencyKey}")));
    }

    public Task<GatewayResult> VoidAsync(VoidPaymentRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GatewayResult.Success());
    }

    public Task<GatewayResult<PaymentCapture>> CaptureAsync(CapturePaymentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GatewayResult<PaymentCapture>.Success(new PaymentCapture($"placeholder-capture-{request.IdempotencyKey}")));
    }

    public Task<GatewayResult> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GatewayResult.Success());
    }
}
