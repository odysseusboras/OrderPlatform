using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using OrderPlatform.Api.Models;
using OrderPlatform.BuildingBlocks.Paging;
using OrderPlatform.Catalog.Contracts;

namespace OrderPlatform.Api.Controllers;

/// <summary>What can be bought, and at which price.</summary>
[ApiController]
[Route("api/offers")]
public sealed class OffersController(ICatalogReader catalog, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Lists the products on sale with their current price, one page at a time, ordered by SKU.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<OfferResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<OfferResponse>>> GetOffers(
        [FromQuery, Range(1, int.MaxValue)] int pageNumber = 1,
        [FromQuery, Range(1, PageRequest.MaxPageSize)] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var page = await catalog.GetOffersAsync(new PageRequest(pageNumber, pageSize), timeProvider.GetUtcNow(), cancellationToken);

        return Ok(PagedResponse<OfferResponse>.From(page.Map(OfferResponse.From)));
    }

    /// <summary>Gets one product on sale with its current price.</summary>
    [HttpGet("{productId:guid}")]
    [ProducesResponseType<OfferResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OfferResponse>> GetOffer(Guid productId, CancellationToken cancellationToken)
    {
        var offers = await catalog.GetOffersAsync([productId], timeProvider.GetUtcNow(), cancellationToken);

        return offers.Count == 1 ? Ok(OfferResponse.From(offers[0])) : NotFound();
    }
}
