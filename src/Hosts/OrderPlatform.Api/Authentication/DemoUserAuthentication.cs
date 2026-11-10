using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OrderPlatform.Api.Hosting;

namespace OrderPlatform.Api.Authentication;

/// <summary>The roles the API knows. A user without a role is a customer.</summary>
public static class ApiRoles
{
    /// <summary>The carrier or the back office: may confirm that an order was delivered.</summary>
    public const string Carrier = "Carrier";
}

/// <summary>
/// Sign-in for Development and tests only: the <c>X-User-Id</c> header is the user and the optional
/// <c>X-User-Role</c> header is their role. Lets the demo show owner-only
/// access without an identity provider. Token authentication replaces it in milestone M5.
/// </summary>
public sealed class DemoUserAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DemoUser";
    public const string HeaderName = "X-User-Id";
    public const string RoleHeaderName = "X-User-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var header))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Guid.TryParse(header.ToString(), out var userId) || userId == Guid.Empty)
        {
            return Task.FromResult(AuthenticateResult.Fail($"{HeaderName} must be a user id (a GUID)."));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], SchemeName);
        if (Request.Headers.TryGetValue(RoleHeaderName, out var role) && !string.IsNullOrWhiteSpace(role))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role.ToString()));
        }

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

internal static class AuthenticationSetup
{
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IHostEnvironment environment)
    {
        if (!environment.IsDevelopmentOrTesting())
        {
            throw new InvalidOperationException(
                $"Only the demo sign-in exists so far, and it is allowed in Development and {ApiEnvironments.Testing} only. " +
                $"Configure token authentication (milestone M5) before running in {environment.EnvironmentName}.");
        }

        services.AddAuthentication(DemoUserAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, DemoUserAuthenticationHandler>(DemoUserAuthenticationHandler.SchemeName, configureOptions: null);
        services.AddAuthorization();

        return services;
    }
}
