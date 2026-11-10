using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using OrderPlatform.Api.Authentication;

namespace OrderPlatform.Integration.Tests.Http;

/// <summary>The API docs must be usable as they are: the sign-in headers are described and an order is ready to send.</summary>
public sealed class ApiDocumentationTests : IDisposable
{
    private readonly ApiFactory _api = new();

    [Fact]
    public async Task Document_DescribesTheDemoSignInHeaders_AndRequiresThem()
    {
        var document = await DocumentAsync();

        var headers = document.Components!.SecuritySchemes!.Values.Select(scheme => (scheme.Type, scheme.In, scheme.Name)).ToList();
        Assert.Contains((SecuritySchemeType.ApiKey, ParameterLocation.Header, DemoUserAuthenticationHandler.HeaderName), headers);
        Assert.Contains((SecuritySchemeType.ApiKey, ParameterLocation.Header, DemoUserAuthenticationHandler.RoleHeaderName), headers);
        Assert.Equal(2, Assert.Single(document.Security!).Count);
    }

    [Fact]
    public async Task Document_PlaceOrder_HasAReadyToSendExample()
    {
        var document = await DocumentAsync();

        var body = document.Paths["/api/orders"].Operations![HttpMethod.Post].RequestBody!.Content!["application/json"];
        Assert.Equal("tok_visa", body.Example!["paymentToken"]!.GetValue<string>());
        Assert.Equal(2, body.Example["lines"]!.AsArray().Count);
    }

    [Fact]
    public async Task Document_PlaceOrder_OffersToWaitForThePrice()
    {
        var document = await DocumentAsync();

        var prefer = Assert.Single(document.Paths["/api/orders"].Operations![HttpMethod.Post].Parameters!, parameter => parameter.Name == "Prefer");
        Assert.Equal("wait=5", prefer.Example!.GetValue<string>());
        Assert.False(prefer.Required);
    }

    private Task<OpenApiDocument> DocumentAsync() =>
        _api.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

    public void Dispose() => _api.Dispose();
}
