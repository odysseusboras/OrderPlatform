using Microsoft.Extensions.DependencyInjection;
using OrderPlatform.Integrations.Payments.Contracts;

namespace OrderPlatform.Integrations.Payments;

public static class PaymentsIntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentsIntegration(this IServiceCollection services) =>
        services.AddScoped<IPaymentGateway, PaymentGateway>();
}
