namespace OrderPlatform.Ordering.Orders;

public sealed record ShippingAddress(string Recipient, string Street, string City, string PostalCode, string CountryCode);
