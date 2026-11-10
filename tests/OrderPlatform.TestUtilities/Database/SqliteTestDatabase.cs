using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using OrderPlatform.BuildingBlocks.Entities;

namespace OrderPlatform.TestUtilities.Database;

/// <summary>
/// An in-memory SQLite database with the entity configurations of the given modules, registered as the shared
/// <see cref="DbContext"/> the modules use. Lives as long as this object. SQL Server specifics (temporal tables,
/// schemas, generated row versions) are covered by the persistence model tests.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ModuleModel _model;

    public SqliteTestDatabase(params Assembly[] moduleAssemblies)
    {
        _connection.Open();
        _model = new ModuleModel(moduleAssemblies);
    }

    public IServiceCollection Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(_model);
        services.AddDbContext<DbContext, ModuleTestDbContext>(options => options
            .UseSqlite(_connection)
            .ReplaceService<IModelCacheKeyFactory, ModuleModelCacheKeyFactory>());

        return services;
    }

    /// <summary>Creates the tables. Call once, after the service provider is built.</summary>
    public static void Create(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<DbContext>().Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();
}

public sealed record ModuleModel(IReadOnlyList<Assembly> Assemblies)
{
    public string Key => string.Join("|", Assemblies.Select(assembly => assembly.FullName));
}

internal sealed class ModuleTestDbContext(DbContextOptions<ModuleTestDbContext> options, ModuleModel model) : DbContext(options)
{
    public ModuleModel Modules { get; } = model;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var assembly in Modules.Assemblies)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }

        // Only real entities, as in AppDbContext; touching any other discovered type would make EF map it as an entity.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(type => typeof(IEntity).IsAssignableFrom(type.ClrType)).ToList())
        {
            var entity = modelBuilder.Entity(entityType.ClrType);

            // As in AppDbContext: ids are created in code, never by the database.
            foreach (var key in entityType.FindPrimaryKey()?.Properties ?? [])
            {
                entity.Property(key.Name).ValueGeneratedNever();
            }

            // SQL Server generates row versions; SQLite cannot, so the initial value is sent as it is.
            foreach (var rowVersion in entityType.GetProperties().Where(property => property.IsConcurrencyToken && property.ClrType == typeof(byte[])).ToList())
            {
                entity.Property(rowVersion.Name).ValueGeneratedNever();
            }
        }
    }

    // SQLite cannot compare DateTimeOffset values; stored as numbers, date filters and ordering run in the database as on SQL Server.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<Microsoft.EntityFrameworkCore.Storage.ValueConversion.DateTimeOffsetToBinaryConverter>();
}

/// <summary>Different test classes can use different modules: one cached model per set of modules.</summary>
internal sealed class ModuleModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        (context.GetType(), ((ModuleTestDbContext)context).Modules.Key, designTime);
}
