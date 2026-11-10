using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.BuildingBlocks.Paging;

namespace OrderPlatform.Ordering.Orders;

public interface IOrderRepository
{
    void Add(Order order);

    /// <summary>Loads the order with its items and status history, tracked for changes.</summary>
    Task<Order?> FindAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>One page of the customer's orders, newest first, paged in the database.</summary>
    Task<Page<OrderSummary>> ListForCustomerAsync(Guid customerId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Saves every pending change of the unit of work (orders and pipeline runs) in one transaction.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record OrderSummary(Guid OrderId, OrderStatus Status, Currency Currency, decimal? TotalAmount, int ItemCount, DateTimeOffset CreatedAt);
