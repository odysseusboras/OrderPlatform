using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using OrderPlatform.Api.Controllers;
using OrderPlatform.Api.Models;
using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.BuildingBlocks.Paging;
using OrderPlatform.Catalog.Contracts;

namespace OrderPlatform.Integration.Tests.Controllers;

public sealed class OffersControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Offer Keyboard = new(Guid.NewGuid(), "KEYBOARD-MX", "Mechanical keyboard", true, new Money(79.90m, Currency.EUR), 0.24m);

    private readonly ICatalogReader _catalog = Substitute.For<ICatalogReader>();
    private readonly OffersController _controller;

    public OffersControllerTests() => _controller = new OffersController(_catalog, new FakeTimeProvider(Now));

    [Fact]
    public async Task GetOffers_ReturnsThePage_WithNetVatAndGrossPrice()
    {
        _catalog.GetOffersAsync(Arg.Is<PageRequest>(page => page.PageNumber == 2 && page.PageSize == 5), Now, Arg.Any<CancellationToken>())
            .Returns(new Page<Offer>([Keyboard], 2, 5, 6));

        var response = await _controller.GetOffers(2, 5, CancellationToken.None);

        var page = Assert.IsType<PagedResponse<OfferResponse>>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal((2, 5, 6, 2, false), (page.PageNumber, page.PageSize, page.TotalCount, page.TotalPages, page.HasNextPage));
        var offer = Assert.Single(page.Items);
        Assert.Equal(Currency.EUR, offer.Currency);
        Assert.Equal(79.90m, offer.NetPrice);
        Assert.Equal(19.18m, offer.VatAmount);
        Assert.Equal(99.08m, offer.GrossPrice);
    }

    [Fact]
    public async Task GetOffer_Known_ReturnsIt()
    {
        _catalog.GetOffersAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == Keyboard.ProductId), Now, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Offer>>([Keyboard]));

        var response = await _controller.GetOffer(Keyboard.ProductId, CancellationToken.None);

        Assert.Equal("KEYBOARD-MX", Assert.IsType<OfferResponse>(Assert.IsType<OkObjectResult>(response.Result).Value).Sku);
    }

    [Fact]
    public async Task GetOffer_Unknown_ReturnsNotFound()
    {
        _catalog.GetOffersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Now, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Offer>>([]));

        var response = await _controller.GetOffer(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(response.Result);
    }
}
