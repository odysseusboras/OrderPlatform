namespace OrderPlatform.ProcessEngine.Pipelines;

public sealed record RetryPolicy(int MaxAttempts, TimeSpan BaseDelay, TimeSpan MaxDelay);
