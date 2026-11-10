namespace OrderPlatform.ProcessEngine.Steps;

/// <summary>
/// One unit of work in a pipeline. A step performs a single action and, when it is compensatable, its undo.
/// A step only changes the context; the runner persists the outcome.
/// </summary>
public interface IPipelineStep<in TContext>
    where TContext : class
{
    bool ShouldRun(TContext context);

    Task<StepResult> ExecuteAsync(TContext context, CancellationToken cancellationToken);

    Task<StepResult> CompensateAsync(TContext context, CancellationToken cancellationToken);
}
