using Microsoft.Extensions.Logging;
using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.ProcessEngine.Execution;

internal static partial class PipelineLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Run {RunId}: step {StepKey} ({Kind}) finished with {Outcome}")]
    public static partial void StepFinished(ILogger logger, Guid runId, string stepKey, string kind, StepOutcome outcome);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Run {RunId} needs attention: {Reason}")]
    public static partial void RequiresAttention(ILogger logger, Guid runId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Run {RunId} starts compensating ({Cause}): {Reason}")]
    public static partial void CompensationStarted(ILogger logger, Guid runId, string cause, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Run {RunId}: step {StepKey} threw an unexpected exception")]
    public static partial void StepThrew(ILogger logger, Exception exception, Guid runId, string stepKey);
}
