using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OrderPlatform.BuildingBlocks.Entities;
using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.Catalog.Products;
using OrderPlatform.Ordering.Orders;
using OrderPlatform.ProcessEngine.Runs;

namespace OrderPlatform.Persistence;

/// <summary>
/// The single database context. Each module owns its entity configurations and its schema;
/// this context only applies them and sets conventions shared by all modules.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Product).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Order).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PipelineRun).Assembly);

        AssignEveryIdInCode(modelBuilder);
        MakeEveryEntityTableTemporal(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<Currency>().HaveConversion<string>().HaveMaxLength(3);
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    /// <summary>
    /// <see cref="Entity"/> creates its id (a version 7 Guid) when the object is created. Without this,
    /// EF treats a new child with an id, added to a loaded aggregate, as an existing row and updates instead of inserting.
    /// </summary>
    private static void AssignEveryIdInCode(ModelBuilder modelBuilder)
    {
        foreach (var entityType in EntityTypes(modelBuilder))
        {
            modelBuilder.Entity(entityType.ClrType).Property(nameof(IEntity.Id)).ValueGeneratedNever();
        }
    }

    /// <summary>
    /// SQL Server system-versioned tables keep every previous version of every row in a history table,
    /// so any change to an entity can be traced and queried later (FOR SYSTEM_TIME).
    /// Owned types live in their owner's table and are versioned with it.
    /// </summary>
    private static void MakeEveryEntityTableTemporal(ModelBuilder modelBuilder)
    {
        foreach (var entityType in EntityTypes(modelBuilder))
        {
            var tableName = entityType.GetTableName()
                ?? throw new InvalidOperationException($"{entityType.ClrType.Name} is not mapped to a table.");

            modelBuilder.Entity(entityType.ClrType)
                .ToTable(tableName, entityType.GetSchema(), table => table.IsTemporal());
        }
    }

    private static List<IMutableEntityType> EntityTypes(ModelBuilder modelBuilder) =>
        modelBuilder.Model.GetEntityTypes()
            .Where(type => !type.IsOwned() && typeof(IEntity).IsAssignableFrom(type.ClrType))
            .ToList();
}
