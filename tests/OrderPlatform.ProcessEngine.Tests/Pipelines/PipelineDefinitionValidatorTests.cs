using OrderPlatform.ProcessEngine.Pipelines;
using OrderPlatform.ProcessEngine.Tests.Support;
using static OrderPlatform.ProcessEngine.Tests.Support.TestPipeline;

namespace OrderPlatform.ProcessEngine.Tests.Pipelines;

public sealed class PipelineDefinitionValidatorTests
{
    [Fact]
    public void Validate_StandardShape_Passes() =>
        PipelineDefinitionValidator.Validate(Standard());

    [Fact]
    public void Validate_OnlyForwardOnlySteps_Passes() =>
        PipelineDefinitionValidator.Validate(new TestPipeline(1, Step<StepFour>(StepKind.ForwardOnly)));

    [Fact]
    public void Validate_VersionZero_Fails() =>
        AssertInvalid(new TestPipeline(0, Step<StepOne>()), "version");

    [Fact]
    public void Validate_NoSteps_Fails() =>
        AssertInvalid(new TestPipeline(1), "at least one step");

    [Fact]
    public void Validate_DuplicateKey_Fails() =>
        AssertInvalid(new TestPipeline(1, Step<StepOne>(), Step<StepOne>()), "more than once");

    [Fact]
    public void Validate_StepTypeIsNotAStepOfTheContext_Fails() =>
        AssertInvalid(
            new TestPipeline(1, new PipelineStepDefinition("wrong", typeof(string), StepKind.Compensatable, TriggerMode.Auto, ThreeAttempts)),
            "does not implement");

    [Theory]
    [InlineData(0, 1, 10)]
    [InlineData(3, -1, 10)]
    [InlineData(3, 20, 10)]
    public void Validate_InvalidRetryPolicy_Fails(int maxAttempts, int baseSeconds, int maxSeconds) =>
        AssertInvalid(
            new TestPipeline(1, Step<StepOne>(retry: new RetryPolicy(maxAttempts, TimeSpan.FromSeconds(baseSeconds), TimeSpan.FromSeconds(maxSeconds)))),
            "retry policy");

    [Fact]
    public void Validate_ZeroTimeout_Fails() =>
        AssertInvalid(new TestPipeline(1, Step<StepOne>(timeout: TimeSpan.Zero)), "timeout");

    [Fact]
    public void Validate_TwoPivots_Fails() =>
        AssertInvalid(new TestPipeline(1, Step<StepOne>(StepKind.Pivot), Step<StepTwo>(StepKind.Pivot)), "Only one step");

    [Fact]
    public void Validate_CompensatableStepAfterThePivot_Fails() =>
        AssertInvalid(new TestPipeline(1, Step<StepOne>(StepKind.Pivot), Step<StepTwo>()), "before the pivot");

    [Fact]
    public void Validate_ForwardOnlyStepBeforeACompensatableOne_Fails() =>
        AssertInvalid(new TestPipeline(1, Step<StepOne>(StepKind.ForwardOnly), Step<StepTwo>()), "before the pivot");

    private static void AssertInvalid(TestPipeline pipeline, string expected)
    {
        var error = Assert.Throws<InvalidOperationException>(() => PipelineDefinitionValidator.Validate(pipeline));
        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }
}
