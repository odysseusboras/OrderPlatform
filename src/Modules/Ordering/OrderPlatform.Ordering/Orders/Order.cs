using OrderPlatform.BuildingBlocks.Auditing;
using OrderPlatform.BuildingBlocks.Money;

namespace OrderPlatform.Ordering.Orders;

public sealed record NewOrderLine(Guid ProductId, string Sku, int Quantity, bool IsPhysical);

public sealed class Order : AuditableEntity
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Priced, OrderStatus.Rejected, OrderStatus.Cancelled],
        [OrderStatus.Priced] = [OrderStatus.Confirmed, OrderStatus.Rejected, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Paid, OrderStatus.Failed, OrderStatus.Cancelled],
        [OrderStatus.Paid] = [OrderStatus.Shipped, OrderStatus.Failed, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered, OrderStatus.Failed, OrderStatus.Cancelled],
    };

    private readonly List<OrderItem> _items = [];
    private readonly List<OrderStatusHistory> _statusHistory = [];

    private Order()
    {
    }

    public Guid CustomerId { get; private set; }

    public OrderStatus Status { get; private set; }

    public Currency Currency { get; private set; }

    public ShippingAddress ShippingAddress { get; private set; } = null!;

    /// <summary>The payment provider's token for the customer's payment method. Never card data.</summary>
    public string PaymentToken { get; private set; } = string.Empty;

    public decimal? SubtotalAmount { get; private set; }

    public decimal? TaxAmount { get; private set; }

    public decimal? ShippingChargeAmount { get; private set; }

    public decimal? TotalAmount { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyList<OrderItem> Items => _items;

    public IReadOnlyList<OrderStatusHistory> StatusHistory => _statusHistory;

    public Money? Total => TotalAmount is { } amount ? new Money(amount, Currency) : null;

    /// <summary>Customer cancellation is allowed until the payment is captured (the saga's pivot).</summary>
    public bool CanCancel => Status is OrderStatus.Pending or OrderStatus.Priced or OrderStatus.Confirmed or OrderStatus.Paid or OrderStatus.Shipped;

    public static Order Create(
        Guid customerId,
        ShippingAddress shippingAddress,
        string paymentToken,
        Currency currency,
        IReadOnlyCollection<NewOrderLine> lines)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("An order needs a customer.", nameof(customerId));
        }

        ArgumentNullException.ThrowIfNull(shippingAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentToken);
        if (!Enum.IsDefined(currency))
        {
            throw new ArgumentOutOfRangeException(nameof(currency), currency, "Unknown currency.");
        }

        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            throw new ArgumentException("An order needs at least one product.", nameof(lines));
        }

        if (lines.Any(line => line.Quantity <= 0))
        {
            throw new ArgumentException("Every quantity must be greater than zero.", nameof(lines));
        }

        if (lines.DistinctBy(line => line.ProductId).Count() != lines.Count)
        {
            throw new ArgumentException("A product can appear only once; use the quantity instead.", nameof(lines));
        }

        var order = new Order
        {
            CustomerId = customerId,
            ShippingAddress = shippingAddress,
            PaymentToken = paymentToken,
            Currency = currency,
            Status = OrderStatus.Pending,
        };

        order._items.AddRange(lines.Select(line => new OrderItem(order.Id, line.ProductId, line.Sku, line.Quantity, line.IsPhysical)));
        order._statusHistory.Add(new OrderStatusHistory(order.Id, 1, null, OrderStatus.Pending, null));

        return order;
    }

    public bool CanTransitionTo(OrderStatus next) =>
        AllowedTransitions.TryGetValue(Status, out var allowed) && allowed.Contains(next);

    public void TransitionTo(OrderStatus next, string? reason = null)
    {
        if (!CanTransitionTo(next))
        {
            throw new InvalidOperationException($"An order cannot move from {Status} to {next}.");
        }

        _statusHistory.Add(new OrderStatusHistory(Id, _statusHistory.Count + 1, Status, next, reason));
        Status = next;
    }

    /// <summary>Stores the price snapshot calculated by the pipeline and moves the order to Priced.</summary>
    public void ApplyPrice(Money subtotal, Money tax, Money shippingCharge, Money total)
    {
        Money[] amounts = [subtotal, tax, shippingCharge, total];
        if (amounts.Any(amount => amount.Currency != Currency))
        {
            throw new ArgumentException($"Every amount must be in the order currency {Currency}.");
        }

        if (amounts.Any(amount => amount.Amount < 0))
        {
            throw new ArgumentException("Amounts cannot be negative.");
        }

        TransitionTo(OrderStatus.Priced);
        SubtotalAmount = subtotal.Amount;
        TaxAmount = tax.Amount;
        ShippingChargeAmount = shippingCharge.Amount;
        TotalAmount = total.Amount;
    }
}
