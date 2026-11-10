namespace OrderPlatform.Api.Hosting;

public static class ApiEnvironments
{
    /// <summary>The environment the integration tests run the API in.</summary>
    public const string Testing = "Testing";

    /// <summary>Development and the integration tests use the demo sign-in and the fake external systems.</summary>
    public static bool IsDevelopmentOrTesting(this IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment(Testing);
}
