using OrderPlatform.Api.Orders;

namespace OrderPlatform.Integration.Tests.Orders;

public sealed class PreferWaitTests
{
    private static readonly TimeSpan Max = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("wait=5", 5)]
    [InlineData("WAIT=5", 5)]
    [InlineData("wait = 5", 5)]
    [InlineData("wait=\"5\"", 5)]
    [InlineData("respond-async, wait=7", 7)]
    [InlineData("return=representation; wait=2", 2)]
    [InlineData("wait=10", 10)]
    [InlineData("wait=60", 10)]
    public void Read_WaitPreference_ReturnsTheWaitCappedAtTheMaximum(string prefer, int expectedSeconds) =>
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), PreferWait.Read(prefer, Max));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("respond-async")]
    [InlineData("wait=0")]
    [InlineData("wait=-3")]
    [InlineData("wait=soon")]
    [InlineData("wait=1.5")]
    public void Read_NoUsableWait_DoesNotWait(string? prefer) =>
        Assert.Null(PreferWait.Read(prefer, Max));

    [Fact]
    public void Applied_WritesTheWaitInSeconds() =>
        Assert.Equal("wait=4", PreferWait.Applied(TimeSpan.FromSeconds(4)));
}
