using Microsoft.Extensions.Time.Testing;
using OrderPlatform.BuildingBlocks.Integration;
using OrderPlatform.FakeExternalSystems;
using OrderPlatform.FakeExternalSystems.Inventory;
using OrderPlatform.Integrations.Inventory.Contracts;

namespace OrderPlatform.Ordering.Tests.Fakes;

public sealed class FakeInventoryGatewayTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeExternalSystemsOptions _options = new();
    private readonly FakeInventoryGateway _inventory;

    public FakeInventoryGatewayTests() => _inventory = new FakeInventoryGateway(_options, new FakeTimeProvider(Now));

    [Fact]
    public async Task Reserve_InStock_ReturnsAReservationThatExpiresLater()
    {
        var result = await ReserveAsync("key-1", "KEYBOARD-MX");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.ExpiresAt > Now);
        Assert.True(_inventory.IsReserved(result.Value.ReservationId));
    }

    [Fact]
    public async Task Reserve_SameKeyTwice_ReturnsTheSameReservation()
    {
        var first = await ReserveAsync("key-1", "KEYBOARD-MX");
        var second = await ReserveAsync("key-1", "KEYBOARD-MX");

        Assert.Equal(first.Value, second.Value);
    }

    [Fact]
    public async Task Reserve_DifferentKeys_ReturnDifferentReservations()
    {
        var first = await ReserveAsync("key-1", "KEYBOARD-MX");
        var second = await ReserveAsync("key-2", "KEYBOARD-MX");

        Assert.NotEqual(first.Value!.ReservationId, second.Value!.ReservationId);
    }

    [Fact]
    public async Task Reserve_SkuConfiguredOutOfStock_FailsForGood_NamingTheSku()
    {
        _options.Inventory.OutOfStockSkus.Add("monitor-27");

        var result = await ReserveAsync("key-1", "KEYBOARD-MX", "MONITOR-27");

        Assert.Equal(GatewayOutcome.PermanentFailure, result.Outcome);
        Assert.Equal("Out of stock: MONITOR-27.", result.Error);
    }

    [Fact]
    public async Task Reserve_SkuConfiguredUnavailable_FailsForNow()
    {
        _options.Inventory.UnavailableSkus.Add("KEYBOARD-MX");

        var result = await ReserveAsync("key-1", "KEYBOARD-MX");

        Assert.Equal(GatewayOutcome.TransientFailure, result.Outcome);
    }

    [Fact]
    public async Task Release_Reserved_FreesIt_AndASecondReleaseIsHarmless()
    {
        var reservation = (await ReserveAsync("key-1", "KEYBOARD-MX")).Value!;

        var first = await _inventory.ReleaseAsync(new ReleaseStockRequest("release-1", reservation.ReservationId), CancellationToken.None);
        var second = await _inventory.ReleaseAsync(new ReleaseStockRequest("release-1", reservation.ReservationId), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.False(_inventory.IsReserved(reservation.ReservationId));
    }

    [Fact]
    public async Task Consume_Reserved_Succeeds_AndUnknownReservationFailsForGood()
    {
        var reservation = (await ReserveAsync("key-1", "KEYBOARD-MX")).Value!;

        var consumed = await _inventory.ConsumeAsync(new ConsumeStockRequest("consume-1", reservation.ReservationId), CancellationToken.None);
        var unknown = await _inventory.ConsumeAsync(new ConsumeStockRequest("consume-2", "res-unknown"), CancellationToken.None);

        Assert.True(consumed.IsSuccess);
        Assert.Equal(GatewayOutcome.PermanentFailure, unknown.Outcome);
    }

    [Fact]
    public async Task Reserve_CancelledToken_Throws()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _inventory.ReserveAsync(new ReserveStockRequest("key-1", Guid.CreateVersion7(), [new StockLine("KEYBOARD-MX", 1)]), cancelled.Token));
    }

    private Task<GatewayResult<StockReservation>> ReserveAsync(string key, params string[] skus) =>
        _inventory.ReserveAsync(
            new ReserveStockRequest(key, Guid.CreateVersion7(), skus.Select(sku => new StockLine(sku, 1)).ToList()),
            CancellationToken.None);
}
