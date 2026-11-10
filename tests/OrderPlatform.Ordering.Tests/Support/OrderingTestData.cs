using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.Catalog.Contracts;
using OrderPlatform.Ordering.Fulfilment;
using OrderPlatform.Ordering.Orders;

namespace OrderPlatform.Ordering.Tests.Support;

internal static class OrderingTestData
{
    public static readonly Guid Alice = new("30000000-0000-0000-0000-000000000001");
    public static readonly Guid Bob = new("30000000-0000-0000-0000-000000000002");

    public static readonly Offer Keyboard = new(new Guid("20000000-0000-0000-0000-000000000003"), "KEYBOARD-MX", "Mechanical keyboard", true, Eur(79.90m), 0.24m);
    public static readonly Offer Ebook = new(new Guid("20000000-0000-0000-0000-000000000007"), "EBOOK-DDD", "Domain-Driven Design e-book", false, Eur(39.00m), 0.06m);
    public static readonly Offer DollarMug = new(new Guid("20000000-0000-0000-0000-000000000099"), "MUG-USD", "Mug sold in dollars", true, new Money(9.00m, Currency.USD), 0.24m);

    public static readonly ShippingAddress Address = new("Ada Lovelace", "1 Main St", "Athens", "10557", "GR");

    public static readonly PriceSnapshot KeyboardPrice = new(Eur(79.90m), Eur(19.18m), Eur(5.00m), Eur(104.08m));

    public static Money Eur(decimal amount) => new(amount, Currency.EUR);

    public static Order NewOrder(Guid? customerId = null) =>
        Order.Create(customerId ?? Alice, Address, "tok_visa", Currency.EUR, [new NewOrderLine(Keyboard.ProductId, Keyboard.Sku, 1, true)]);
}
