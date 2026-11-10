using OrderPlatform.Ordering.Fulfilment;
using OrderPlatform.ProcessEngine.Pipelines;
using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.Ordering.Tests.Fulfilment;

public sealed class OrderFulfilmentPipelineV1Tests
{
    private readonly OrderFulfilmentPipelineV1 _pipeline = new();

    [Fact]
    public void Identity_IsOrderFulfilmentVersion1()
    {
        Assert.Equal("OrderFulfilment", _pipeline.Name);
        Assert.Equal(1, _pipeline.Version);
    }

    [Fact]
    public void Steps_AreInSagaOrder_StockIsReservedBeforeThePriceIsCalculated()
    {
        string[] expected =
        [
            "ReserveStock", "CalculatePrice", "AuthorizePayment", "PrepareShipment",
            "CapturePayment", "DispatchShipment", "AwaitDelivery",
        ];

        Assert.Equal(expected, _pipeline.Steps.Select(step => step.Key));
    }

    [Fact]
    public void Steps_HaveUniqueKeysAndTypes()
    {
        Assert.Equal(_pipeline.Steps.Count, _pipeline.Steps.Select(step => step.Key).Distinct().Count());
        Assert.Equal(_pipeline.Steps.Count, _pipeline.Steps.Select(step => step.StepType).Distinct().Count());
    }

    [Fact]
    public void Steps_HaveExactlyOnePivot_AwaitDelivery_SoEverythingBeforeTheDeliveryCanBeUndone()
    {
        var pivot = Assert.Single(_pipeline.Steps, step => step.Kind == StepKind.Pivot);

        Assert.Equal("AwaitDelivery", pivot.Key);
        Assert.Equal(pivot, _pipeline.Steps[^1]);
    }

    [Fact]
    public void StepsBeforePivot_AreCompensatable_AndStepsAfterPivot_AreForwardOnly()
    {
        var pivotIndex = _pipeline.Steps.ToList().FindIndex(step => step.Kind == StepKind.Pivot);

        Assert.All(_pipeline.Steps.Take(pivotIndex), step => Assert.Equal(StepKind.Compensatable, step.Kind));
        Assert.All(_pipeline.Steps.Skip(pivotIndex + 1), step => Assert.Equal(StepKind.ForwardOnly, step.Kind));
    }

    [Fact]
    public void AwaitDelivery_IsTheOnlyOnDemandStep_AndHasATimeout()
    {
        var onDemand = Assert.Single(_pipeline.Steps, step => step.Trigger == TriggerMode.OnDemand);

        Assert.Equal("AwaitDelivery", onDemand.Key);
        Assert.NotNull(onDemand.Timeout);
    }

    [Fact]
    public void EveryStepType_ImplementsTheStepContractForTheFulfilmentContext()
    {
        Assert.All(
            _pipeline.Steps,
            step => Assert.True(typeof(IPipelineStep<OrderFulfilmentContext>).IsAssignableFrom(step.StepType), step.Key));
    }

    [Fact]
    public void EveryRetryPolicy_IsValid()
    {
        Assert.All(_pipeline.Steps, step =>
        {
            Assert.True(step.Retry.MaxAttempts >= 1, step.Key);
            Assert.True(step.Retry.BaseDelay <= step.Retry.MaxDelay, step.Key);
        });
    }
}
