using OrderPlatform.BuildingBlocks.Integration;
using OrderPlatform.Ordering.Fulfilment.Steps;
using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.Ordering.Tests.Fulfilment;

public sealed class GatewayStepResultsTests
{
    [Fact]
    public void ToStepResult_Succeeded_IsSuccess() =>
        Assert.Equal(StepResult.Success(), GatewayResult.Success().ToStepResult());

    [Fact]
    public void ToStepResult_TransientFailure_IsRetryableWithTheError() =>
        Assert.Equal(StepResult.Retryable("timeout"), GatewayResult.Transient("timeout").ToStepResult());

    [Fact]
    public void ToStepResult_PermanentFailure_IsFatalWithTheError() =>
        Assert.Equal(StepResult.Fatal("declined"), GatewayResult.Permanent("declined").ToStepResult());

    [Fact]
    public void ToStepResult_FailureWithoutDetails_StillHasAnError() =>
        Assert.False(string.IsNullOrWhiteSpace(new GatewayResult(GatewayOutcome.PermanentFailure).ToStepResult().Error));

    [Fact]
    public void IdempotencyKey_IsOrderStepAndAction()
    {
        var orderId = Guid.CreateVersion7();

        Assert.Equal($"{orderId}:ReserveStock:Execute", GatewayStepResults.IdempotencyKey(orderId, "ReserveStock", "Execute"));
    }
}
