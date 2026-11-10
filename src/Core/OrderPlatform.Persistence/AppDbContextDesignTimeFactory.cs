using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderPlatform.Persistence;

/// <summary>Used only by the EF Core tools to create migrations. The connection string is never opened.</summary>
internal sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DesignTimeConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=OrderPlatform;Trusted_Connection=True";

    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(DesignTimeConnectionString).Options);
}
