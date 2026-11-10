using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.ProcessEngine.Pipelines;

/// <summary>Checks a pipeline definition when it is registered, so a wrong definition stops the application at startup.</summary>
public static class PipelineDefinitionValidator
{
    public static void Validate<TContext>(PipelineDefinition<TContext> definition)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(definition);
        var errors = Errors(definition).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Pipeline {definition.Name} v{definition.Version} is invalid: {string.Join(" ", errors)}");
        }
    }

    private static IEnumerable<string> Errors<TContext>(PipelineDefinition<TContext> definition)
        where TContext : class
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            yield return "It needs a name.";
        }

        if (definition.Version < 1)
        {
            yield return "The version must be 1 or higher.";
        }

        if (definition.Steps is not { Count: > 0 })
        {
            yield return "It needs at least one step.";
            yield break;
        }

        foreach (var key in definition.Steps.GroupBy(step => step.Key).Where(group => group.Count() > 1).Select(group => group.Key))
        {
            yield return $"Step key '{key}' is used more than once.";
        }

        foreach (var step in definition.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Key))
            {
                yield return "Every step needs a key.";
            }

            if (!typeof(IPipelineStep<TContext>).IsAssignableFrom(step.StepType))
            {
                yield return $"Step '{step.Key}' ({step.StepType.Name}) does not implement IPipelineStep<{typeof(TContext).Name}>.";
            }

            if (step.Retry.MaxAttempts < 1 || step.Retry.BaseDelay < TimeSpan.Zero || step.Retry.BaseDelay > step.Retry.MaxDelay)
            {
                yield return $"Step '{step.Key}' has an invalid retry policy.";
            }

            if (step.Timeout is { } timeout && timeout <= TimeSpan.Zero)
            {
                yield return $"Step '{step.Key}' has a timeout that is not positive.";
            }
        }

        if (definition.Steps.Count(step => step.Kind == StepKind.Pivot) > 1)
        {
            yield return "Only one step can be the pivot.";
        }

        var kinds = definition.Steps.Select(step => step.Kind).ToList();
        if (!kinds.SequenceEqual(kinds.Order()))
        {
            yield return "Compensatable steps must come before the pivot, and forward-only steps after it.";
        }
    }
}
