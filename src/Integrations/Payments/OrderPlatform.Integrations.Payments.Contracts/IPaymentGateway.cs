using OrderPlatform.BuildingBlocks.Integration;

namespace OrderPlatform.Integrations.Payments.Contracts;

/// <summary>The platform's view of the payment provider. Works with provider tokens only, never card data.</summary>
public interface IPaymentGateway
{
    Task<GatewayResult<PaymentAuthorization>> AuthorizeAsync(AuthorizePaymentRequest request, CancellationToken cancellationToken);

    Task<GatewayResult> VoidAsync(VoidPaymentRequest request, CancellationToken cancellationToken);

    Task<GatewayResult<PaymentCapture>> CaptureAsync(CapturePaymentRequest request, CancellationToken cancellationToken);

    /// <summary>Gives captured money back to the customer.</summary>
    Task<GatewayResult> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken);
}
