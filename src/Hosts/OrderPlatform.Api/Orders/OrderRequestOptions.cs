using System.ComponentModel.DataAnnotations;

namespace OrderPlatform.Api.Orders;

public sealed class OrderRequestOptions
{
    public const string SectionName = "Orders";

    /// <summary>The longest a client may ask <c>POST /api/orders</c> to wait for the price (<c>Prefer: wait</c>).</summary>
    [Range(1, 30)]
    public int MaxWaitSeconds { get; set; } = 10;

    public TimeSpan MaxWait => TimeSpan.FromSeconds(MaxWaitSeconds);
}

internal static class OrderRequestSetup
{
    public static IServiceCollection AddOrderRequests(this IServiceCollection services)
    {
        services.AddOptions<OrderRequestOptions>()
            .BindConfiguration(OrderRequestOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
