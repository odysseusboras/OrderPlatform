using OrderPlatform.ProcessEngine.Execution;
using OrderPlatform.ProcessEngine.Runs;

namespace OrderPlatform.ProcessEngine.Tests.Support;

/// <summary>Records what an observer sees after every move, and can change the context like a business module would.</summary>
public sealed class RecordingObserver : IPipelineObserver<FlowContext>
{
    public List<(RunStatus Status, int NextStepIndex, List<string> Visited)> Calls { get; } = [];

    public string? RenameTo { get; set; }

    public Task OnAdvancedAsync(PipelineRun run, FlowContext context, CancellationToken cancellationToken)
    {
        Calls.Add((run.Status, run.NextStepIndex, [.. context.Visited]));
        if (RenameTo is not null)
        {
            context.Name = RenameTo;
        }

        return Task.CompletedTask;
    }
}
