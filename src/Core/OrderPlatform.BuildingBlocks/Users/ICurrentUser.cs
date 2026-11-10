namespace OrderPlatform.BuildingBlocks.Users;

/// <summary>The user on whose behalf the current operation runs. Recorded in the audit fields.</summary>
public interface ICurrentUser
{
    Guid UserId { get; }
}
