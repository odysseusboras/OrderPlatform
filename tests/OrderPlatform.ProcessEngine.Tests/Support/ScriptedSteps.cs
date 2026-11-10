using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.ProcessEngine.Tests.Support;

/// <summary>Records the order in which steps run and are undone, across every step of a test.</summary>
public sealed class StepJournal
{
    public List<string> Entries { get; } = [];
}

/// <summary>
/// A step whose results are queued by the test. With nothing queued it succeeds.
/// A queued action can return a result or throw, to imitate a real step.
/// </summary>
public abstract class ScriptedStep(StepJournal journal, string name) : IPipelineStep<FlowContext>
{
    private readonly Queue<Func<CancellationToken, StepResult>> _executions = new();
    private readonly Queue<Func<CancellationToken, StepResult>> _compensations = new();

    public bool Runs { get; set; } = true;

    public int ExecuteCalls { get; private set; }

    public int CompensateCalls { get; private set; }

    public ScriptedStep ThenExecute(params StepResult[] results)
    {
        foreach (var result in results)
        {
            _executions.Enqueue(_ => result);
        }

        return this;
    }

    public ScriptedStep ThenExecute(Func<CancellationToken, StepResult> action)
    {
        _executions.Enqueue(action);
        return this;
    }

    public ScriptedStep ThenCompensate(params StepResult[] results)
    {
        foreach (var result in results)
        {
            _compensations.Enqueue(_ => result);
        }

        return this;
    }

    public bool ShouldRun(FlowContext context) => Runs;

    public Task<StepResult> ExecuteAsync(FlowContext context, CancellationToken cancellationToken)
    {
        ExecuteCalls++;
        journal.Entries.Add($"execute {name}");
        var result = Next(_executions, cancellationToken);
        if (result.Outcome == StepOutcome.Success)
        {
            context.Visited.Add(name);
        }

        return Task.FromResult(result);
    }

    public Task<StepResult> CompensateAsync(FlowContext context, CancellationToken cancellationToken)
    {
        CompensateCalls++;
        journal.Entries.Add($"compensate {name}");
        return Task.FromResult(Next(_compensations, cancellationToken));
    }

    private static StepResult Next(Queue<Func<CancellationToken, StepResult>> queue, CancellationToken cancellationToken) =>
        queue.TryDequeue(out var action) ? action(cancellationToken) : StepResult.Success();
}

public sealed class StepOne(StepJournal journal) : ScriptedStep(journal, "one");

public sealed class StepTwo(StepJournal journal) : ScriptedStep(journal, "two");

public sealed class StepThree(StepJournal journal) : ScriptedStep(journal, "three");

public sealed class StepFour(StepJournal journal) : ScriptedStep(journal, "four");

public sealed class StepFive(StepJournal journal) : ScriptedStep(journal, "five");
