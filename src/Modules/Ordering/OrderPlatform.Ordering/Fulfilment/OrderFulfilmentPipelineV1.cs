using OrderPlatform.Ordering.Fulfilment.Steps;
using OrderPlatform.ProcessEngine.Pipelines;

namespace OrderPlatform.Ordering.Fulfilment;

/// <summary>
/// Version 1 of the order fulfilment saga. Released: do not edit. Add <c>OrderFulfilmentPipelineV2</c> instead.
/// </summary>
public sealed class OrderFulfilmentPipelineV1 : PipelineDefinition<OrderFulfilmentContext>
{
    public const string PipelineName = "OrderFulfilment";

    private static readonly RetryPolicy Quick = new(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
    private static readonly RetryPolicy External = new(5, TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(1));
    private static readonly RetryPolicy Persistent = new(100, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(15));
    private static readonly RetryPolicy None = new(1, TimeSpan.Zero, TimeSpan.Zero);

    public override string Name => PipelineName;

    public override int Version => 1;

    public override IReadOnlyList<PipelineStepDefinition> Steps { get; } =
    [
        new(Step01ReserveStock.Key, typeof(Step01ReserveStock), StepKind.Compensatable, TriggerMode.Auto, External),
        new(Step02CalculatePrice.Key, typeof(Step02CalculatePrice), StepKind.Compensatable, TriggerMode.Auto, Quick),
        new(Step03AuthorizePayment.Key, typeof(Step03AuthorizePayment), StepKind.Compensatable, TriggerMode.Auto, External),
        new(Step04PrepareShipment.Key, typeof(Step04PrepareShipment), StepKind.Compensatable, TriggerMode.Auto, External),
        new(Step05CapturePayment.Key, typeof(Step05CapturePayment), StepKind.Compensatable, TriggerMode.Auto, External),
        new(Step06DispatchShipment.Key, typeof(Step06DispatchShipment), StepKind.Compensatable, TriggerMode.Auto, External),
        new(Step07AwaitDelivery.Key, typeof(Step07AwaitDelivery), StepKind.Pivot, TriggerMode.OnDemand, External, TimeSpan.FromDays(14)),
    ];
}
