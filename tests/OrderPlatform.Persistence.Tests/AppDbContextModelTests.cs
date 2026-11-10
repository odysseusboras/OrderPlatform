using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using OrderPlatform.Catalog.Products;
using OrderPlatform.Ordering.Orders;
using OrderPlatform.ProcessEngine.Runs;

namespace OrderPlatform.Persistence.Tests;

/// <summary>Checks the database model without connecting to a database.</summary>
public sealed class AppDbContextModelTests : IDisposable
{
    private readonly AppDbContext _context = new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=unused;Database=unused").Options);

    // The design-time model keeps every configuration, including temporal table settings.
    private IModel Model => _context.GetService<IDesignTimeModel>().Model;

    [Theory]
    [InlineData(typeof(TaxCategory), "catalog", "TaxCategories")]
    [InlineData(typeof(Product), "catalog", "Products")]
    [InlineData(typeof(ProductPrice), "catalog", "ProductPrices")]
    [InlineData(typeof(Order), "ordering", "Orders")]
    [InlineData(typeof(OrderItem), "ordering", "OrderItems")]
    [InlineData(typeof(OrderStatusHistory), "ordering", "OrderStatusChanges")]
    [InlineData(typeof(PipelineRun), "process", "PipelineRuns")]
    [InlineData(typeof(StepExecution), "process", "StepExecutions")]
    [InlineData(typeof(StepAttempt), "process", "StepAttempts")]
    public void EveryEntity_IsInItsModuleSchema(Type entity, string schema, string table)
    {
        var entityType = Model.FindEntityType(entity)!;

        Assert.Equal(schema, entityType.GetSchema());
        Assert.Equal(table, entityType.GetTableName());
    }

    [Fact]
    public void EveryEntityId_IsAssignedInCode_NotByTheDatabase()
    {
        var entities = Model.GetEntityTypes().Where(type => !type.IsOwned()).ToList();

        Assert.All(entities, type => Assert.Equal(ValueGenerated.Never, type.FindPrimaryKey()!.Properties.Single().ValueGenerated));
    }

    [Fact]
    public void PipelineRun_IsUniquePerPipelineAndBusinessKey_AndUsesOptimisticConcurrency()
    {
        var run = Model.FindEntityType(typeof(PipelineRun))!;

        Assert.Contains(run.GetIndexes(), index => index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual([nameof(PipelineRun.PipelineName), nameof(PipelineRun.BusinessKey)]));
        Assert.True(run.FindProperty(nameof(PipelineRun.RowVersion))!.IsConcurrencyToken);
    }

    [Fact]
    public void EveryEntityTable_IsTemporal_AndKeepsItsHistoryInItsModuleSchema()
    {
        var entities = Model.GetEntityTypes().Where(type => !type.IsOwned()).ToList();

        Assert.NotEmpty(entities);
        Assert.All(entities, type =>
        {
            Assert.True(type.IsTemporal(), type.ClrType.Name);
            Assert.Equal(type.GetSchema(), type.GetHistoryTableSchema());
            Assert.Equal(type.GetTableName() + "History", type.GetHistoryTableName());
        });
    }

    [Theory]
    [InlineData(typeof(Order), nameof(Order.Status))]
    [InlineData(typeof(OrderStatusHistory), nameof(OrderStatusHistory.FromStatus))]
    [InlineData(typeof(OrderStatusHistory), nameof(OrderStatusHistory.ToStatus))]
    [InlineData(typeof(PipelineRun), nameof(PipelineRun.Status))]
    [InlineData(typeof(StepExecution), nameof(StepExecution.Kind))]
    [InlineData(typeof(StepAttempt), nameof(StepAttempt.Outcome))]
    public void Enums_AreStoredAsStrings(Type entity, string property)
    {
        var mapped = Model.FindEntityType(entity)!.FindProperty(property)!;

        Assert.Equal(typeof(string), mapped.GetProviderClrType());
    }

    [Theory]
    [InlineData(typeof(Order), nameof(Order.Currency))]
    [InlineData(typeof(ProductPrice), nameof(ProductPrice.Currency))]
    public void Currencies_AreStoredAsTheirThreeLetterCode(Type entity, string property)
    {
        var mapped = Model.FindEntityType(entity)!.FindProperty(property)!;

        Assert.Equal(typeof(string), mapped.GetProviderClrType());
        Assert.Equal(3, mapped.GetMaxLength());
    }

    [Fact]
    public void Order_UsesARowVersionForConcurrency()
    {
        var rowVersion = Model.FindEntityType(typeof(Order))!.FindProperty(nameof(Order.RowVersion))!;

        Assert.True(rowVersion.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
    }

    [Fact]
    public void Order_StoresTheShippingAddressInTheOrdersTable()
    {
        var address = Model.FindEntityType(typeof(Order))!.FindComplexProperty(nameof(Order.ShippingAddress));

        Assert.NotNull(address);
        Assert.Null(Model.FindEntityType(typeof(ShippingAddress)));
    }

    [Fact]
    public void Amounts_HaveTwoDecimals_AndTaxRatesFour()
    {
        var total = Model.FindEntityType(typeof(Order))!.FindProperty(nameof(Order.TotalAmount))!;
        var rate = Model.FindEntityType(typeof(TaxCategory))!.FindProperty(nameof(TaxCategory.Rate))!;

        Assert.Equal((18, 2), (total.GetPrecision(), total.GetScale()));
        Assert.Equal((5, 4), (rate.GetPrecision(), rate.GetScale()));
    }

    [Theory]
    [InlineData(typeof(Order), nameof(Order.Total))]
    [InlineData(typeof(ProductPrice), nameof(ProductPrice.Price))]
    public void CalculatedMoneyProperties_AreNotMapped(Type entity, string property)
    {
        Assert.Null(Model.FindEntityType(entity)!.FindProperty(property));
    }

    [Fact]
    public void Migrations_AreUpToDateWithTheModel()
    {
        Assert.False(_context.Database.HasPendingModelChanges(), "Run 'dotnet ef migrations add' for the model changes.");
        Assert.Contains(_context.Database.GetMigrations(), migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
    }

    public void Dispose() => _context.Dispose();
}
