using OrderPlatform.ProcessEngine.Execution;
using OrderPlatform.ProcessEngine.Pipelines;

namespace OrderPlatform.ProcessEngine.Tests.Execution;

public sealed class RetryScheduleTests
{
    private static readonly RetryPolicy Policy = new(10, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30));

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    [InlineData(5, 30)]
    [InlineData(60, 30)]
    public void DelayAfter_DoublesTheBaseDelay_UpToTheMaximum(int attempt, int expectedSeconds) =>
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), RetrySchedule.DelayAfter(Policy, attempt));

    [Fact]
    public void DelayAfter_ZeroBaseDelay_RetriesImmediately() =>
        Assert.Equal(TimeSpan.Zero, RetrySchedule.DelayAfter(new RetryPolicy(3, TimeSpan.Zero, TimeSpan.Zero), 2));
}
