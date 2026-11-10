using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.ProcessEngine.Tests.Steps;

public sealed class StepResultTests
{
    [Fact]
    public void Success_HasSuccessOutcome_AndNoError()
    {
        var result = StepResult.Success();

        Assert.Equal(StepOutcome.Success, result.Outcome);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Retryable_CarriesTheError()
    {
        var result = StepResult.Retryable("provider unavailable");

        Assert.Equal(StepOutcome.RetryableError, result.Outcome);
        Assert.Equal("provider unavailable", result.Error);
    }

    [Fact]
    public void Fatal_CarriesTheError()
    {
        var result = StepResult.Fatal("card declined");

        Assert.Equal(StepOutcome.FatalError, result.Outcome);
        Assert.Equal("card declined", result.Error);
    }

    [Fact]
    public void Waiting_HasWaitingOutcome()
    {
        Assert.Equal(StepOutcome.Waiting, StepResult.Waiting().Outcome);
    }

    [Fact]
    public void Interrupted_HasInterruptedOutcome()
    {
        Assert.Equal(StepOutcome.Interrupted, StepResult.Interrupted().Outcome);
    }
}
