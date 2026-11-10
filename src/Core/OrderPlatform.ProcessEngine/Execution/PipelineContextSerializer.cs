using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderPlatform.ProcessEngine.Execution;

/// <summary>Stores a run's context as JSON. Enums are written by name, so the stored context stays readable.</summary>
public static class PipelineContextSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize<TContext>(TContext context)
        where TContext : class =>
        JsonSerializer.Serialize(context, Options);

    public static TContext Deserialize<TContext>(string json)
        where TContext : class =>
        JsonSerializer.Deserialize<TContext>(json, Options)
        ?? throw new InvalidOperationException($"The stored context is not a valid {typeof(TContext).Name}.");
}
