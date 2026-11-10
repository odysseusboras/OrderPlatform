using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using OrderPlatform.BuildingBlocks.Users;
using OrderPlatform.Persistence.Auditing;

namespace OrderPlatform.Persistence.Tests;

public sealed class PersistenceServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPersistence_RegistersTheContext_WithTheAuditInterceptor()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<ICurrentUser, SystemCurrentUser>()
            .AddPersistence("Server=unused;Database=unused")
            .BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var interceptors = context.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()!.Interceptors!;

        Assert.Contains(interceptors, interceptor => interceptor is AuditInterceptor);
    }

    [Fact]
    public void AddPersistence_ExposesTheSameContextAsDbContext_ForTheModules()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<ICurrentUser, SystemCurrentUser>()
            .AddPersistence("Server=unused;Database=unused")
            .BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Same(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<DbContext>());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AddPersistence_WithoutConnectionString_Throws(string connectionString)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ServiceCollection().AddPersistence(connectionString));
    }
}
