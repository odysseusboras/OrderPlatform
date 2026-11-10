using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OrderPlatform.Api.Hosting;
using OrderPlatform.Catalog.Contracts;
using OrderPlatform.Ordering.Placing;

namespace OrderPlatform.Integration.Tests.Http;

/// <summary>
/// Hosts the real API in memory: routing, validation, rate limiting and error handling run as in production.
/// The catalog and the order service are replaced, so no database is needed.
/// </summary>
public sealed class ApiFactory(int permitLimit = 1000) : WebApplicationFactory<Program>
{
    public ICatalogReader Catalog { get; } = Substitute.For<ICatalogReader>();

    public IOrderService Orders { get; } = Substitute.For<IOrderService>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(ApiEnvironments.Testing);
        builder.UseSetting("ConnectionStrings:OrderPlatform", "Server=unused;Database=unused");
        builder.UseSetting("RateLimiting:PermitLimit", permitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.ConfigureTestServices(services => services.AddSingleton(Catalog).AddSingleton(Orders));
    }
}
