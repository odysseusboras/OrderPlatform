using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace OrderPlatform.Api.Errors;

/// <summary>
/// Turns every exception that escapes a controller into RFC 9457 problem details with a safe message.
/// Unexpected errors are logged with their details; the client only gets a generic message and the trace id to quote.
/// </summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = Describe(httpContext, exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnexpected(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title },
        });
    }

    private static (int Status, string Title) Describe(HttpContext httpContext, Exception exception) => exception switch
    {
        BadHttpRequestException badRequest => (badRequest.StatusCode, "The request could not be read. Check the URL, headers and body."),
        OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested => (ClientClosedRequest, "The client closed the request."),
        DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "The resource was changed by someone else. Reload it and try again."),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred. Quote the trace id when reporting it."),
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnexpected(ILogger logger, Exception exception, string method, string path);
}
