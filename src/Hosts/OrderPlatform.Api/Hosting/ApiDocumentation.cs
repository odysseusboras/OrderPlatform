using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using OrderPlatform.Api.Authentication;
using OrderPlatform.Api.Models;
using OrderPlatform.Api.Orders;
using Scalar.AspNetCore;

namespace OrderPlatform.Api.Hosting;

/// <summary>
/// The OpenAPI document and the Scalar page that shows it (Development only). The document describes the demo
/// sign-in headers and carries a ready-to-send order, so every endpoint can be tried from the page without typing.
/// </summary>
internal static class ApiDocumentation
{
    private const string UserScheme = "DemoUser";
    private const string RoleScheme = "DemoRole";

    // The demo customer the page signs in as, and an order of two seeded products.
    private const string DemoCustomerId = "30000000-0000-0000-0000-000000000001";
    private const string ExamplePreference = "wait=5";
    private const string ExampleOrder = """
        {
          "lines": [
            { "productId": "20000000-0000-0000-0000-000000000003", "quantity": 1 },
            { "productId": "20000000-0000-0000-0000-000000000005", "quantity": 2 }
          ],
          "shippingAddress": {
            "recipient": "Ada Lovelace",
            "street": "1 Main St",
            "city": "Athens",
            "postalCode": "10557",
            "countryCode": "GR"
          },
          "paymentToken": "tok_visa",
          "holdBeforePayment": false
        }
        """;

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "Order Processing Platform";
                document.Info.Description =
                    "Demo sign-in: the X-User-Id header is the user (any GUID). "
                    + "Confirming a delivery also needs the X-User-Role header with the value Carrier.";

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[UserScheme] = HeaderScheme(
                    DemoUserAuthenticationHandler.HeaderName, "The signed-in user: any GUID. Development and tests only.");
                document.Components.SecuritySchemes[RoleScheme] = HeaderScheme(
                    DemoUserAuthenticationHandler.RoleHeaderName, $"Optional role. Use {ApiRoles.Carrier} to confirm a delivery.");

                document.Security ??= [];
                document.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(UserScheme, document)] = [],
                    [new OpenApiSecuritySchemeReference(RoleScheme, document)] = [],
                });

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                var placesAnOrder = context.Description.ParameterDescriptions.Any(parameter => parameter.Type == typeof(CreateOrderRequest));
                if (placesAnOrder && operation.RequestBody?.Content is { } content)
                {
                    foreach (var mediaType in content.Values)
                    {
                        mediaType.Example = JsonNode.Parse(ExampleOrder);
                    }
                }

                // The page offers "wait for the price" filled in, so a new order comes back priced (201).
                // The API itself still does not wait unless the client asks: clear the header to get the 202.
                if (placesAnOrder && operation.Parameters?.OfType<OpenApiParameter>().FirstOrDefault(parameter => parameter.Name == PreferWait.HeaderName) is { } prefer)
                {
                    prefer.Example = JsonValue.Create(ExamplePreference);
                    prefer.Description = "Optional. wait=N asks the API to calculate the price within the request, for at most N seconds. Without it the order is accepted (202) and priced in the background.";
                }

                return Task.CompletedTask;
            });
        });

    public static void MapApiDocumentation(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options => options
            .WithTitle("Order Processing Platform")
            .AddPreferredSecuritySchemes(UserScheme, RoleScheme)
            .AddApiKeyAuthentication(UserScheme, key => key.Value = DemoCustomerId)
            .AddApiKeyAuthentication(RoleScheme, key => key.Value = ApiRoles.Carrier));
    }

    private static OpenApiSecurityScheme HeaderScheme(string header, string description) => new()
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = header,
        Description = description,
    };
}
