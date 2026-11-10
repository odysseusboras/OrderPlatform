using OrderPlatform.Ordering.Orders;
using OrderPlatform.ProcessEngine.Execution;
using OrderPlatform.ProcessEngine.Runs;

namespace OrderPlatform.Ordering.Fulfilment;

/// <summary>Updates the order after every move of its fulfilment run, in the same transaction as the run.</summary>
internal sealed class OrderProgressObserver(IOrderRepository orders) : IPipelineObserver<OrderFulfilmentContext>
{
    public async Task OnAdvancedAsync(PipelineRun run, OrderFulfilmentContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(context);

        var order = await orders.FindAsync(context.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Run {run.Id} belongs to order {context.OrderId}, which does not exist.");

        var passedSteps = run.Steps
            .Where(step => step.Kind == StepExecutionKind.Execute && step.Status is StepExecutionStatus.Succeeded or StepExecutionStatus.Skipped)
            .Select(step => step.StepKey)
            .ToHashSet();

        OrderProgress.Apply(order, run.Status, run.CompensationCause, run.Reason, context.Price, passedSteps);
    }
}
