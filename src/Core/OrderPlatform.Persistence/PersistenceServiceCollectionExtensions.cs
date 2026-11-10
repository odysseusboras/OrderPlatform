using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderPlatform.Persistence.Auditing;

namespace OrderPlatform.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers the database context with the audit interceptor.
    /// Modules use it through the base <see cref="DbContext"/> type, so they never reference this project.
    /// The host registers <see cref="BuildingBlocks.Users.ICurrentUser"/>: the token user in the API, the system user in the worker.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AuditInterceptor>();
        services.AddDbContext<AppDbContext>((provider, options) => options
            .UseSqlServer(connectionString)
            .AddInterceptors(provider.GetRequiredService<AuditInterceptor>()));
        services.AddScoped<DbContext>(provider => provider.GetRequiredService<AppDbContext>());

        return services;
    }

    /// <summary>Applies pending migrations. Development only: production runs a migration bundle in the deployment pipeline.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database;
        await database.MigrateAsync(cancellationToken);
    }
}
