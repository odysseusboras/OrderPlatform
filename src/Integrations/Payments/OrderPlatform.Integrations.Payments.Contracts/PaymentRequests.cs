using OrderPlatform.BuildingBlocks.Money;

namespace OrderPlatform.Integrations.Payments.Contracts;

public sealed record AuthorizePaymentRequest(string IdempotencyKey, Guid OrderId, string PaymentToken, Money Amount);

public sealed record PaymentAuthorization(string AuthorizationId);

public sealed record VoidPaymentRequest(string IdempotencyKey, string AuthorizationId);

public sealed record CapturePaymentRequest(string IdempotencyKey, string AuthorizationId, Money Amount);

public sealed record PaymentCapture(string CaptureId);

public sealed record RefundPaymentRequest(string IdempotencyKey, string CaptureId, Money Amount);
