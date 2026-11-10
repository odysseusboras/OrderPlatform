using OrderPlatform.BuildingBlocks.Users;
using OrderPlatform.Catalog;
using OrderPlatform.FakeExternalSystems;
using OrderPlatform.Integrations.Inventory;
using OrderPlatform.Integrations.Payments;
using OrderPlatform.Integrations.Shipping;
using OrderPlatform.Ordering;
using OrderPlatform.Persistence;
using OrderPlatform.Pricing;
using OrderPlatform.ProcessEngine;

// The worker host: moves due fulfilment runs forward. It has no HTTP surface, so it scales separately from the API.
var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("OrderPlatform")
    ?? throw new InvalidOperationException("The connection string 'OrderPlatform' is missing.");

// Everything the worker saves is audited as the system user.
builder.Services.AddSingleton<ICurrentUser, SystemCurrentUser>();

builder.Services
    .AddPersistence(connectionString)
    .AddCatalogModule()
    .AddPricingModule()
    .AddOrderingModule()
    .AddPipelineWorker();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddFakeExternalSystems(fakes => builder.Configuration.GetSection(FakeExternalSystemsOptions.SectionName).Bind(fakes));
}
else
{
    builder.Services.AddInventoryIntegration().AddPaymentsIntegration().AddShippingIntegration();
}

await builder.Build().RunAsync();
