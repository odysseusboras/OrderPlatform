using OrderPlatform.ProcessEngine.Runs;

namespace OrderPlatform.ProcessEngine.Execution;

/// <summary>
/// Lets the business module follow a run, for example to keep an order's status in line with its fulfilment.
/// Called after every move of a run of this context type, in the same unit of work, so the run and the
/// business changes are saved in one transaction. Must not call <c>SaveChanges</c>.
/// </summary>
public interface IPipelineObserver<in TContext>
    where TContext : class
{
    Task OnAdvancedAsync(PipelineRun run, TContext context, CancellationToken cancellationToken);
}
