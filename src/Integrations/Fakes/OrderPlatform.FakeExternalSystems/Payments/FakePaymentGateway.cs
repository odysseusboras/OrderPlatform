using OrderPlatform.BuildingBlocks.Integration;
using OrderPlatform.Integrations.Payments.Contracts;

namespace OrderPlatform.FakeExternalSystems.Payments;

/// <summary>In-memory stand-in for the payment provider: it accepts every call. Development and tests only.</summary>
internal sealed class FakePaymentGateway : IPaymentGateway
{
    // ponytail: always succeeds; the same key gives the same id, as the real provider must.
    public Task<GatewayResult<PaymentAuthorization>> AuthorizeAsync(AuthorizePaymentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GatewayResult<PaymentAuthorization>.Success(new PaymentAuthorization($"fake-auth-{request.IdempotencyKey}")));
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
        return Task.FromResult(GatewayResult<PaymentCapture>.Success(new PaymentCapture($"fake-capture-{request.IdempotencyKey}")));
    }

    public Task<GatewayResult> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GatewayResult.Success());
    }
}
