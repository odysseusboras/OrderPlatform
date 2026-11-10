using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OrderPlatform.BuildingBlocks.Paging;

namespace OrderPlatform.Integration.Tests.Http;

/// <summary>Every failure comes back as problem details with a trace id and a safe message.</summary>
public sealed class ErrorResponsesTests : IDisposable
{
    private readonly ApiFactory _api = new();

    [Theory]
    [InlineData("/api/offers?pageSize=101")]
    [InlineData("/api/offers?pageSize=0")]
    [InlineData("/api/offers?pageNumber=0")]
    public async Task GetOffers_OutsideThePagingLimits_Returns400ValidationProblem(string url)
    {
        var response = await _api.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotEmpty(problem!.Errors);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task UnknownOffer_Returns404Problem()
    {
        _api.Catalog.GetOffersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OrderPlatform.Catalog.Contracts.Offer>>([]));

        var response = await _api.CreateClient().GetAsync($"/api/offers/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnknownRoute_Returns404Problem()
    {
        var response = await _api.CreateClient().GetAsync("/api/does-not-exist");

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnexpectedException_Returns500Problem_WithoutInternalDetails()
    {
        _api.Catalog.GetOffersAsync(Arg.Any<PageRequest>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("connection string Password=secret"));

        var response = await _api.CreateClient().GetAsync("/api/offers");

        var problem = await AssertProblemAsync(response, HttpStatusCode.InternalServerError);
        Assert.Equal("An unexpected error occurred. Quote the trace id when reporting it.", problem.Title);
        Assert.DoesNotContain("secret", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _api.Dispose();

    private static async Task<ProblemDetails> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal((int)status, problem!.Status);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
        return problem;
    }
}
