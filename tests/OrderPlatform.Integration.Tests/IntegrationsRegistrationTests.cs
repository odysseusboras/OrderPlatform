using Microsoft.Extensions.DependencyInjection;
using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.Integrations.Inventory;
using OrderPlatform.Integrations.Inventory.Contracts;
using OrderPlatform.Integrations.Payments;
using OrderPlatform.Integrations.Payments.Contracts;
using OrderPlatform.Integrations.Shipping;
using OrderPlatform.Integrations.Shipping.Contracts;

namespace OrderPlatform.Integration.Tests;

public sealed class IntegrationsRegistrationTests
{
    [Fact]
    public void AddIntegrations_RegisterTheRealGateways()
    {
        using var provider = new ServiceCollection()
            .AddInventoryIntegration()
            .AddPaymentsIntegration()
            .AddShippingIntegration()
            .BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.StartsWith("OrderPlatform.Integrations.Inventory", scope.ServiceProvider.GetRequiredService<IInventoryGateway>().GetType().Namespace);
        Assert.StartsWith("OrderPlatform.Integrations.Payments", scope.ServiceProvider.GetRequiredService<IPaymentGateway>().GetType().Namespace);
        Assert.StartsWith("OrderPlatform.Integrations.Shipping", scope.ServiceProvider.GetRequiredService<IShippingGateway>().GetType().Namespace);
    }

    [Fact]
    public async Task InventoryGateway_Placeholder_AcceptsEveryCall_WithTheSameReservationForTheSameKey()
    {
        using var provider = new ServiceCollection().AddInventoryIntegration().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var inventory = scope.ServiceProvider.GetRequiredService<IInventoryGateway>();
        var request = new ReserveStockRequest("order-1:ReserveStock:Execute", Guid.CreateVersion7(), [new StockLine("KEYBOARD-MX", 1)]);

        var first = await inventory.ReserveAsync(request, CancellationToken.None);
        var second = await inventory.ReserveAsync(request, CancellationToken.None);
        var released = await inventory.ReleaseAsync(new ReleaseStockRequest("order-1:ReserveStock:Compensate", first.Value!.ReservationId), CancellationToken.None);
        var consumed = await inventory.ConsumeAsync(new ConsumeStockRequest("order-1:Consume", first.Value.ReservationId), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value, second.Value);
        Assert.True(released.IsSuccess);
        Assert.True(consumed.IsSuccess);
    }

    [Fact]
    public async Task PaymentGateway_Placeholder_AcceptsEveryCall_WithTheSameIdsForTheSameKey()
    {
        using var provider = new ServiceCollection().AddPaymentsIntegration().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var payments = scope.ServiceProvider.GetRequiredService<IPaymentGateway>();
        var amount = new Money(104.08m, Currency.EUR);
        var authorize = new AuthorizePaymentRequest("order-1:AuthorizePayment:Execute", Guid.CreateVersion7(), "tok_visa", amount);

        var first = await payments.AuthorizeAsync(authorize, CancellationToken.None);
        var second = await payments.AuthorizeAsync(authorize, CancellationToken.None);
        var captured = await payments.CaptureAsync(new CapturePaymentRequest("order-1:CapturePayment:Execute", first.Value!.AuthorizationId, amount), CancellationToken.None);
        var voided = await payments.VoidAsync(new VoidPaymentRequest("order-1:AuthorizePayment:Compensate", first.Value.AuthorizationId), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value, second.Value);
        var refunded = await payments.RefundAsync(new RefundPaymentRequest("order-1:CapturePayment:Compensate", captured.Value!.CaptureId, amount), CancellationToken.None);

        Assert.True(captured.IsSuccess);
        Assert.True(voided.IsSuccess);
        Assert.True(refunded.IsSuccess);
    }

    [Fact]
    public async Task ShippingGateway_Placeholder_AcceptsEveryCall_WithTheSameBookingForTheSameKey()
    {
        using var provider = new ServiceCollection().AddShippingIntegration().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var shipping = scope.ServiceProvider.GetRequiredService<IShippingGateway>();
        var book = new BookShipmentRequest(
            "order-1:PrepareShipment:Execute", Guid.CreateVersion7(), new ShippingAddress("Ada", "1 Main St", "Athens", "10557", "GR"), [new ShipmentLine("KEYBOARD-MX", 1)]);

        var first = await shipping.BookAsync(book, CancellationToken.None);
        var second = await shipping.BookAsync(book, CancellationToken.None);
        var dispatched = await shipping.DispatchAsync(new DispatchShipmentRequest("order-1:DispatchShipment:Execute", first.Value!.ShipmentId), CancellationToken.None);
        var cancelled = await shipping.CancelAsync(new CancelShipmentRequest("order-1:PrepareShipment:Compensate", first.Value.ShipmentId), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value, second.Value);
        Assert.StartsWith("TRK-", first.Value.TrackingNumber, StringComparison.Ordinal);
        var recalled = await shipping.RecallAsync(new RecallShipmentRequest("order-1:DispatchShipment:Compensate", first.Value.ShipmentId), CancellationToken.None);

        Assert.True(dispatched.IsSuccess);
        Assert.True(cancelled.IsSuccess);
        Assert.True(recalled.IsSuccess);
    }
}
