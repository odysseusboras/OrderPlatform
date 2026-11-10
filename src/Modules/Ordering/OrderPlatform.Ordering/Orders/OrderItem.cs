using OrderPlatform.BuildingBlocks.Auditing;

namespace OrderPlatform.Ordering.Orders;

public sealed class OrderItem : AuditableEntity
{
    private OrderItem()
    {
    }

    internal OrderItem(Guid orderId, Guid productId, string sku, int quantity, bool isPhysical)
    {
        OrderId = orderId;
        ProductId = productId;
        Sku = sku;
        Quantity = quantity;
        IsPhysical = isPhysical;
    }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    public bool IsPhysical { get; private set; }
}
