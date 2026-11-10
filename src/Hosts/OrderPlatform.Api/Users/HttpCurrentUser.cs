using System.Security.Claims;
using OrderPlatform.BuildingBlocks.Users;

namespace OrderPlatform.Api.Users;

/// <summary>
/// The user from the request's token. Read only when something is saved, so anonymous reads work.
/// Work outside a request (the background worker hosted in the API) is done as the system user.
/// </summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            if (httpContextAccessor.HttpContext is not { } httpContext)
            {
                return SystemUser.Id;
            }

            var subject = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(subject, out var userId)
                ? userId
                : throw new InvalidOperationException("The request has no authenticated user, so the change cannot be audited.");
        }
    }
}
