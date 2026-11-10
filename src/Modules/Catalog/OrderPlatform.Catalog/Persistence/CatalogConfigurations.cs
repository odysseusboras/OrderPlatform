using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderPlatform.Catalog.Products;

namespace OrderPlatform.Catalog.Persistence;

internal static class CatalogSchema
{
    public const string Name = "catalog";
}

internal sealed class TaxCategoryConfiguration : IEntityTypeConfiguration<TaxCategory>
{
    public void Configure(EntityTypeBuilder<TaxCategory> builder)
    {
        builder.ToTable("TaxCategories", CatalogSchema.Name);
        builder.Property(category => category.Name).HasMaxLength(50);
        builder.Property(category => category.Rate).HasPrecision(5, 4);
        builder.HasIndex(category => category.Name).IsUnique();
        builder.HasData(CatalogSeedData.TaxCategoryData());
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", CatalogSchema.Name);
        builder.Property(product => product.Sku).HasMaxLength(50);
        builder.Property(product => product.Name).HasMaxLength(200);
        builder.HasIndex(product => product.Sku).IsUnique();
        builder.HasOne<TaxCategory>().WithMany().HasForeignKey(product => product.TaxCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(product => product.Prices).WithOne().HasForeignKey(price => price.ProductId);
        builder.Navigation(product => product.Prices).HasField("_prices");
        builder.HasData(CatalogSeedData.ProductData());
    }
}

internal sealed class ProductPriceConfiguration : IEntityTypeConfiguration<ProductPrice>
{
    public void Configure(EntityTypeBuilder<ProductPrice> builder)
    {
        builder.ToTable("ProductPrices", CatalogSchema.Name);
        builder.Ignore(price => price.Price);
        builder.HasData(CatalogSeedData.ProductPriceData());
    }
}
