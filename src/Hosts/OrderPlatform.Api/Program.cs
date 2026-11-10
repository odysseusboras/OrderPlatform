using System.Text.Json.Serialization;
using OrderPlatform.Api.Authentication;
using OrderPlatform.Api.Errors;
using OrderPlatform.Api.Hosting;
using OrderPlatform.Api.Orders;
using OrderPlatform.Api.RateLimiting;
using OrderPlatform.Api.Users;
using OrderPlatform.BuildingBlocks.Users;
using OrderPlatform.Catalog;
using OrderPlatform.FakeExternalSystems;
using OrderPlatform.Ordering;
using OrderPlatform.Persistence;
using OrderPlatform.Pricing;
using OrderPlatform.ProcessEngine;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("OrderPlatform")
    ?? throw new InvalidOperationException("The connection string 'OrderPlatform' is missing.");

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddApiDocumentation();
builder.Services.AddApiErrorHandling();
builder.Services.AddApiRateLimiting();
builder.Services.AddApiAuthentication(builder.Environment);
builder.Services.AddOrderRequests();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services
    .AddPersistence(connectionString)
    .AddCatalogModule()
    .AddPricingModule()
    .AddOrderingModule();

if (builder.Environment.IsDevelopmentOrTesting())
{
    builder.Services.AddFakeExternalSystems(fakes => builder.Configuration.GetSection(FakeExternalSystemsOptions.SectionName).Bind(fakes));
}

if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue("Pipeline:RunWorkerInApi", true))
{
    // ponytail: the worker runs inside the API so one process is enough for the demo.
    // Production hosts it in OrderPlatform.Worker (AddPipelineWorker there) and scales it separately.
    // Start with --Pipeline:RunWorkerInApi=false to pause it: orders then stop after pricing, which leaves time to cancel one.
    builder.Services.AddPipelineWorker();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping);
    app.MapApiDocumentation();
}

app.MapControllers();

await app.RunAsync();

/// <summary>Visible to the integration tests, which host the API in memory.</summary>
public partial class Program;
