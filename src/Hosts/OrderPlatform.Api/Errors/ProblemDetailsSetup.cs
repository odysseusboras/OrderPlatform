using System.Diagnostics;

namespace OrderPlatform.Api.Errors;

internal static class ProblemDetailsSetup
{
    /// <summary>
    /// One error format for every failure: validation (400), not found (404), rate limited (429), exceptions (500).
    /// Each response carries the trace id that also appears in the logs.
    /// </summary>
    public static IServiceCollection AddApiErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
