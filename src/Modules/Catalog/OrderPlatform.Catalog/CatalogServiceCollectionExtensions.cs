using Microsoft.Extensions.DependencyInjection;
using OrderPlatform.Catalog.Contracts;
using OrderPlatform.Catalog.Offers;

namespace OrderPlatform.Catalog;

public static class CatalogServiceCollectionExtensions
{
    /// <summary>Registers the catalog. Needs the shared <c>DbContext</c> registered by the persistence layer.</summary>
    public static IServiceCollection AddCatalogModule(this IServiceCollection services) =>
        services.AddScoped<ICatalogReader, CatalogReader>();
}
