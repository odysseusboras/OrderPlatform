namespace OrderPlatform.BuildingBlocks.Users;

/// <summary>
/// The well-known user for work done by the platform itself (the saga worker, seeding).
/// A fixed identifier rather than <see cref="Guid.Empty"/>, which usually means "not set".
/// </summary>
public static class SystemUser
{
    public static readonly Guid Id = new("00000000-0000-0000-0000-000000000001");

    public const string UserName = "system";
}

/// <summary>The current user for background processes: always the system user.</summary>
public sealed class SystemCurrentUser : ICurrentUser
{
    public Guid UserId => SystemUser.Id;
}
