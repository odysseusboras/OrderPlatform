using OrderPlatform.BuildingBlocks.Auditing;

namespace OrderPlatform.Ordering.Orders;

/// <summary>One change of an order's status. When and by whom come from the audit fields.</summary>
public sealed class OrderStatusHistory : AuditableEntity
{
    private OrderStatusHistory()
    {
    }

    internal OrderStatusHistory(Guid orderId, int sequence, OrderStatus? fromStatus, OrderStatus toStatus, string? reason)
    {
        OrderId = orderId;
        Sequence = sequence;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = reason;
    }

    public Guid OrderId { get; private set; }

    /// <summary>Orders the changes of one order; several changes can be saved in the same instant.</summary>
    public int Sequence { get; private set; }

    public OrderStatus? FromStatus { get; private set; }

    public OrderStatus ToStatus { get; private set; }

    public string? Reason { get; private set; }
}
