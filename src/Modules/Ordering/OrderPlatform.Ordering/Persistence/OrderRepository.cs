using Microsoft.EntityFrameworkCore;
using OrderPlatform.BuildingBlocks.Paging;
using OrderPlatform.Ordering.Orders;

namespace OrderPlatform.Ordering.Persistence;

internal sealed class OrderRepository(DbContext database) : IOrderRepository
{
    public void Add(Order order) => database.Set<Order>().Add(order);

    public Task<Order?> FindAsync(Guid orderId, CancellationToken cancellationToken) =>
        database.Set<Order>()
            .Include(order => order.Items)
            .Include(order => order.StatusHistory)
            .AsSplitQuery()
            .SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken);

    public async Task<Page<OrderSummary>> ListForCustomerAsync(Guid customerId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        var orders = database.Set<Order>().AsNoTracking().Where(order => order.CustomerId == customerId);

        var total = await orders.CountAsync(cancellationToken);
        var items = await orders
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(order => new OrderSummary(order.Id, order.Status, order.Currency, order.TotalAmount, order.Items.Count, order.CreatedAt))
            .ToListAsync(cancellationToken);

        return new Page<OrderSummary>(items, page.PageNumber, page.PageSize, total);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => database.SaveChangesAsync(cancellationToken);
}
