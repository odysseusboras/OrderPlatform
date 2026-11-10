using OrderPlatform.ProcessEngine.Pipelines;

namespace OrderPlatform.ProcessEngine.Tests.Support;

public sealed class TestPipeline(int version, params PipelineStepDefinition[] steps) : PipelineDefinition<FlowContext>
{
    public const string PipelineName = "test-flow";

    public static readonly RetryPolicy ThreeAttempts = new(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));

    public override string Name => PipelineName;

    public override int Version => version;

    public override IReadOnlyList<PipelineStepDefinition> Steps => steps;

    public static PipelineStepDefinition Step<TStep>(StepKind kind = StepKind.Compensatable, TriggerMode trigger = TriggerMode.Auto, RetryPolicy? retry = null, TimeSpan? timeout = null) =>
        new(typeof(TStep).Name, typeof(TStep), kind, trigger, retry ?? ThreeAttempts, timeout);

    /// <summary>Two compensatable steps, the pivot, then two forward-only steps: the shape of the order pipeline.</summary>
    public static TestPipeline Standard(int version = 1) =>
        new(
            version,
            Step<StepOne>(),
            Step<StepTwo>(),
            Step<StepThree>(StepKind.Pivot),
            Step<StepFour>(StepKind.ForwardOnly),
            Step<StepFive>(StepKind.ForwardOnly));
}
