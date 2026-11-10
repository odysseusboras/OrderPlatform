using OrderPlatform.BuildingBlocks.Integration;
using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.Ordering.Fulfilment.Steps;

/// <summary>The one translation from an external system's answer to a step result, shared by every step.</summary>
internal static class GatewayStepResults
{
    private const string NoDetails = "The external system gave no details.";

    public static StepResult ToStepResult(this GatewayResult result) => result.Outcome switch
    {
        GatewayOutcome.Succeeded => StepResult.Success(),
        GatewayOutcome.TransientFailure => StepResult.Retryable(result.Error ?? NoDetails),
        GatewayOutcome.PermanentFailure => StepResult.Fatal(result.Error ?? NoDetails),
        _ => throw new InvalidOperationException($"Unknown gateway outcome {result.Outcome}."),
    };

    /// <summary>The same key on every retry of the same action, so the external system never acts twice.</summary>
    public static string IdempotencyKey(Guid orderId, string stepKey, string action) => $"{orderId}:{stepKey}:{action}";
}
