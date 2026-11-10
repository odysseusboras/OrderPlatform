using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderPlatform.Ordering.Orders;

namespace OrderPlatform.Ordering.Persistence;

internal static class OrderingSchema
{
    public const string Name = "ordering";
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", OrderingSchema.Name);
        builder.Property(order => order.PaymentToken).HasMaxLength(200);
        builder.Property(order => order.RowVersion).IsRowVersion();
        builder.Ignore(order => order.Total);
        builder.HasIndex(order => order.CustomerId);

        // A complex property (value object): its columns live in the Orders table and are versioned with it.
        builder.ComplexProperty(order => order.ShippingAddress, address =>
        {
            address.Property(a => a.Recipient).HasMaxLength(200);
            address.Property(a => a.Street).HasMaxLength(200);
            address.Property(a => a.City).HasMaxLength(100);
            address.Property(a => a.PostalCode).HasMaxLength(20);
            address.Property(a => a.CountryCode).HasMaxLength(2);
        });

        builder.HasMany(order => order.Items).WithOne().HasForeignKey(item => item.OrderId);
        builder.Navigation(order => order.Items).HasField("_items");

        builder.HasMany(order => order.StatusHistory).WithOne().HasForeignKey(change => change.OrderId);
        builder.Navigation(order => order.StatusHistory).HasField("_statusHistory");
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", OrderingSchema.Name);
        builder.Property(item => item.Sku).HasMaxLength(50);
    }
}

internal sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusChanges", OrderingSchema.Name);
        builder.Property(change => change.Reason).HasMaxLength(500);
        builder.HasIndex(change => new { change.OrderId, change.Sequence }).IsUnique();
    }
}
