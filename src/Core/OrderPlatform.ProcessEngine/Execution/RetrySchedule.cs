using OrderPlatform.ProcessEngine.Pipelines;

namespace OrderPlatform.ProcessEngine.Execution;

internal static class RetrySchedule
{
    /// <summary>Exponential backoff: the base delay doubles after every attempt, up to the maximum delay.</summary>
    public static TimeSpan DelayAfter(RetryPolicy policy, int attempt)
    {
        var factor = Math.Pow(2, Math.Max(0, attempt - 1));
        var delay = TimeSpan.FromTicks((long)Math.Min(policy.BaseDelay.Ticks * factor, policy.MaxDelay.Ticks));
        return delay;
    }
}
