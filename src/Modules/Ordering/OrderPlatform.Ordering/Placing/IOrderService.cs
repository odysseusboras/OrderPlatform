using OrderPlatform.BuildingBlocks.Paging;
using OrderPlatform.BuildingBlocks.Results;
using OrderPlatform.Ordering.Orders;

namespace OrderPlatform.Ordering.Placing;

/// <summary>The order use cases. Customers only ever see and change their own orders.</summary>
public interface IOrderService
{
    /// <summary>
    /// Saves the order (Pending) and starts its fulfilment run in one transaction. When <see cref="PlaceOrderCommand.WaitForPrice"/>
    /// is set, runs the pipeline inside the call up to CalculatePrice (stock is reserved first), for at most that long; the worker continues from there.
    /// </summary>
    Task<Result<PlacedOrder>> PlaceAsync(PlaceOrderCommand command, CancellationToken cancellationToken);

    Task<Result<Order>> GetAsync(Guid orderId, Guid customerId, CancellationToken cancellationToken);

    Task<Page<OrderSummary>> ListAsync(Guid customerId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>
    /// Cancels the order by running its fulfilment backward: every completed step is undone in reverse order
    /// (the parcel is recalled, the payment refunded, the stock released, as far as the order got) and the order becomes
    /// Cancelled. Allowed until the order is delivered (the pivot). If the run is busy elsewhere, the request is recorded and the order is cancelled on its next move.
    /// </summary>
    Task<Result<Order>> CancelAsync(Guid orderId, Guid customerId, CancellationToken cancellationToken);

    /// <summary>
    /// Lets an order that was placed with "hold before payment" continue: the payment is authorized next.
    /// Refused when the order is not on hold.
    /// </summary>
    Task<Result<Order>> ReleaseAsync(Guid orderId, Guid customerId, CancellationToken cancellationToken);

    /// <summary>
    /// Records that a shipped order was delivered (the carrier's or back-office's confirmation) and wakes its fulfilment,
    /// which then completes: the order becomes Delivered.
    /// </summary>
    Task<Result<Order>> ConfirmDeliveryAsync(Guid orderId, CancellationToken cancellationToken);
}

public sealed record PlaceOrderCommand(
    Guid CustomerId,
    IReadOnlyList<RequestedLine> Lines,
    ShippingAddress ShippingAddress,
    string PaymentToken,
    TimeSpan? WaitForPrice,
    bool HoldBeforePayment = false);

public sealed record RequestedLine(Guid ProductId, int Quantity);

/// <param name="Order">The saved order.</param>
/// <param name="PricingFinished">
/// True when the call waited and pricing finished: the order is Priced, or Rejected because an item is out of stock or it could not be priced.
/// False when the call did not wait or the price was not ready in time; the worker goes on.
/// </param>
public sealed record PlacedOrder(Order Order, bool PricingFinished);
