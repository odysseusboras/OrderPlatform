namespace OrderPlatform.Ordering.Orders;

public enum OrderStatus
{
    /// <summary>Received and saved; the fulfilment pipeline has started.</summary>
    Pending,

    /// <summary>The price snapshot (VAT and charges) is stored.</summary>
    Priced,

    /// <summary>Accepted: stock is reserved and the payment is authorized.</summary>
    Confirmed,

    /// <summary>The payment is captured. A cancel from here on refunds it.</summary>
    Paid,

    Shipped,

    Delivered,

    /// <summary>Cancelled by the customer before the delivery; what was reserved, charged or shipped was undone.</summary>
    Cancelled,

    /// <summary>Not accepted: out of stock, declined payment or unknown product.</summary>
    Rejected,

    /// <summary>Accepted, but a later step failed before the delivery and everything was undone, including a refund if the payment was captured.</summary>
    Failed,
}
