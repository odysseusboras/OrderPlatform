using Microsoft.AspNetCore.Mvc;
using OrderPlatform.BuildingBlocks.Results;
using OrderPlatform.Ordering.Placing;

namespace OrderPlatform.Api.Errors;

/// <summary>Turns an expected failure (<see cref="Error"/>) into problem details with the same shape as every other error.</summary>
internal static class ErrorResponses
{
    public static ObjectResult ToProblem(this ControllerBase controller, Error error)
    {
        var status = StatusFor(error);
        var problem = controller.ProblemDetailsFactory.CreateProblemDetails(controller.HttpContext, status, title: error.Message);
        problem.Extensions["code"] = error.Code;

        return new ObjectResult(problem) { StatusCode = status };
    }

    private static int StatusFor(Error error) =>
        error == OrderErrors.NotFound ? StatusCodes.Status404NotFound
        : error == OrderErrors.CannotCancel || error == OrderErrors.NotAwaitingDelivery || error == OrderErrors.NotOnHold ? StatusCodes.Status409Conflict
        : StatusCodes.Status400BadRequest;
}
