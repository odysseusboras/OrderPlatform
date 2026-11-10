namespace OrderPlatform.BuildingBlocks.Integration;

public enum GatewayOutcome
{
    Succeeded,

    /// <summary>The external system may succeed if the same call is retried later (timeout, unavailable).</summary>
    TransientFailure,

    /// <summary>The external system refused the request; retrying will not help (declined, out of stock).</summary>
    PermanentFailure,
}

/// <summary>The outcome of a call to an external system, already translated into the platform's terms.</summary>
public record GatewayResult(GatewayOutcome Outcome, string? Error = null)
{
    public bool IsSuccess => Outcome == GatewayOutcome.Succeeded;

    public static GatewayResult Success() => new(GatewayOutcome.Succeeded);

    public static GatewayResult Transient(string error) => new(GatewayOutcome.TransientFailure, error);

    public static GatewayResult Permanent(string error) => new(GatewayOutcome.PermanentFailure, error);
}

public sealed record GatewayResult<T>(GatewayOutcome Outcome, T? Value = default, string? Error = null)
    : GatewayResult(Outcome, Error)
{
    public static GatewayResult<T> Success(T value) => new(GatewayOutcome.Succeeded, value);

    public static new GatewayResult<T> Transient(string error) => new(GatewayOutcome.TransientFailure, default, error);

    public static new GatewayResult<T> Permanent(string error) => new(GatewayOutcome.PermanentFailure, default, error);
}
