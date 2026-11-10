namespace OrderPlatform.Pricing.Contracts;

public sealed record PriceRequest(IReadOnlyList<PriceRequestLine> Lines);

public sealed record PriceRequestLine(Guid ProductId, int Quantity);
