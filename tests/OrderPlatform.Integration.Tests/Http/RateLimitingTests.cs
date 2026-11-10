using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OrderPlatform.BuildingBlocks.Paging;
using OrderPlatform.Catalog.Contracts;

namespace OrderPlatform.Integration.Tests.Http;

public sealed class RateLimitingTests : IDisposable
{
    private const int PermitLimit = 3;

    private readonly ApiFactory _api = new(PermitLimit);

    public RateLimitingTests() =>
        _api.Catalog.GetOffersAsync(Arg.Any<PageRequest>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new Page<Offer>([], 1, 20, 0));

    [Fact]
    public async Task RequestsAboveTheLimit_Get429Problem_WithRetryAfter()
    {
        var client = _api.CreateClient();
        for (var request = 0; request < PermitLimit; request++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/offers")).StatusCode);
        }

        var rejected = await client.GetAsync("/api/offers");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
        var problem = await rejected.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(429, problem!.Status);
        Assert.Equal("Too many requests. Wait before trying again.", problem.Title);
    }

    public void Dispose() => _api.Dispose();
}
