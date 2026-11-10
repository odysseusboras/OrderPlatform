using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NSubstitute;
using OrderPlatform.Api.Authentication;
using OrderPlatform.Api.Orders;
using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.BuildingBlocks.Paging;
using OrderPlatform.BuildingBlocks.Results;
using OrderPlatform.Ordering.Orders;
using OrderPlatform.Ordering.Placing;

namespace OrderPlatform.Integration.Tests.Http;

/// <summary>The orders endpoints over HTTP: sign-in, validation, status codes and the error format. The order service is a substitute.</summary>
public sealed class OrdersEndpointTests : IDisposable
{
    private static readonly Guid Alice = new("30000000-0000-0000-0000-000000000001");
    private static readonly Guid Keyboard = new("20000000-0000-0000-0000-000000000003");
    private static readonly ShippingAddress Address = new("Ada Lovelace", "1 Main St", "Athens", "10557", "GR");

    private readonly ApiFactory _api = new();
    private readonly HttpClient _client;

    public OrdersEndpointTests()
    {
        _client = _api.CreateClient();
        _client.DefaultRequestHeaders.Add(DemoUserAuthenticationHandler.HeaderName, Alice.ToString());
    }

    [Fact]
    public async Task Create_WithoutSignIn_Is401Problem()
    {
        using var anonymous = _api.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/orders", ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Create_UserIdThatIsNotAGuid_Is401()
    {
        using var client = _api.CreateClient();
        client.DefaultRequestHeaders.Add(DemoUserAuthenticationHandler.HeaderName, "alice");

        var response = await client.PostAsJsonAsync("/api/orders", ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutPrefer_Is202WithLocationAndThePendingOrder_PlacedForTheSignedInCustomerWithoutWaiting()
    {
        var order = NewOrder();
        PlaceReturns(order, pricingFinished: false);

        var response = await _client.PostAsJsonAsync("/api/orders", ValidRequest());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.False(response.Headers.Contains(PreferWait.AppliedHeaderName));
        Assert.EndsWith($"/api/orders/{order.Id}", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.True(body.GetProperty("canCancel").GetBoolean());
        await _api.Orders.Received(1).PlaceAsync(
            Arg.Is<PlaceOrderCommand>(command =>
                command.CustomerId == Alice
                && command.Lines.SequenceEqual(new[] { new RequestedLine(Keyboard, 2) })
                && command.ShippingAddress == Address
                && command.PaymentToken == "tok_visa"
                && command.WaitForPrice == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_PreferWaitAndPricingFinished_Is201_AndReportsTheAppliedWait()
    {
        PlaceReturns(NewOrder(), pricingFinished: true);

        var response = await PostWithPreferAsync("wait=3");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("wait=3", response.Headers.GetValues(PreferWait.AppliedHeaderName).Single());
        await _api.Orders.Received(1).PlaceAsync(Arg.Is<PlaceOrderCommand>(command => command.WaitForPrice == TimeSpan.FromSeconds(3)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_PreferWaitButPriceNotReadyInTime_Is202()
    {
        PlaceReturns(NewOrder(), pricingFinished: false);

        var response = await PostWithPreferAsync("wait=3");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Create_PreferWaitAboveTheMaximum_WaitsTheConfiguredMaximum()
    {
        PlaceReturns(NewOrder(), pricingFinished: true);

        var response = await PostWithPreferAsync("wait=600");

        Assert.Equal("wait=10", response.Headers.GetValues(PreferWait.AppliedHeaderName).Single());
        await _api.Orders.Received(1).PlaceAsync(Arg.Is<PlaceOrderCommand>(command => command.WaitForPrice == TimeSpan.FromSeconds(10)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_CountryCodeInLowerCase_IsSentInUpperCase()
    {
        PlaceReturns(NewOrder(), pricingFinished: false);

        await _client.PostAsJsonAsync("/api/orders", ValidRequest(countryCode: "gr"));

        await _api.Orders.Received(1).PlaceAsync(Arg.Is<PlaceOrderCommand>(command => command.ShippingAddress.CountryCode == "GR"), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public async Task Create_QuantityOutOfRange_Is400ValidationProblem(int quantity)
    {
        var response = await _client.PostAsJsonAsync("/api/orders", ValidRequest(quantity: quantity));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _api.Orders.DidNotReceive().PlaceAsync(Arg.Any<PlaceOrderCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_NoLines_Is400ValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new
        {
            lines = Array.Empty<object>(),
            shippingAddress = Address,
            paymentToken = "tok_visa",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownProduct_Is400ProblemWithTheErrorCode()
    {
        _api.Orders.PlaceAsync(Arg.Any<PlaceOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<PlacedOrder>.Failure(OrderErrors.ProductsNotAvailable([Keyboard])));

        var response = await _client.PostAsJsonAsync("/api/orders", ValidRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ordering.products_not_available", problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Get_NotFound_Is404Problem()
    {
        var orderId = Guid.CreateVersion7();
        _api.Orders.GetAsync(orderId, Alice, Arg.Any<CancellationToken>()).Returns(Result<Order>.Failure(OrderErrors.NotFound));

        var response = await _client.GetAsync($"/api/orders/{orderId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("ordering.order_not_found", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Get_Own_Is200WithItemsAndHistory()
    {
        var order = NewOrder();
        _api.Orders.GetAsync(order.Id, Alice, Arg.Any<CancellationToken>()).Returns(Result<Order>.Success(order));

        var body = await _client.GetFromJsonAsync<JsonElement>($"/api/orders/{order.Id}");

        Assert.Equal("KEYBOARD-MX", body.GetProperty("items")[0].GetProperty("sku").GetString());
        Assert.Equal("Pending", body.GetProperty("statusHistory")[0].GetProperty("to").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("price").ValueKind);
    }

    [Fact]
    public async Task List_PageSizeAboveTheMaximum_Is400()
    {
        var response = await _client.GetAsync("/api/orders?pageSize=500");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsThePageOfTheSignedInCustomer()
    {
        var summary = new OrderSummary(Guid.CreateVersion7(), OrderStatus.Pending, Currency.EUR, null, 1, DateTimeOffset.UnixEpoch);
        _api.Orders.ListAsync(Alice, Arg.Is<PageRequest>(page => page.PageNumber == 2 && page.PageSize == 5), Arg.Any<CancellationToken>())
            .Returns(new Page<OrderSummary>([summary], 2, 5, 6));

        var body = await _client.GetFromJsonAsync<JsonElement>("/api/orders?pageNumber=2&pageSize=5");

        Assert.Equal(6, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(summary.OrderId, body.GetProperty("items")[0].GetProperty("orderId").GetGuid());
    }

    [Fact]
    public async Task Cancel_Cancelled_Is200WithTheCancelledOrder()
    {
        var order = NewOrder();
        order.TransitionTo(OrderStatus.Cancelled, "Cancellation requested.");
        _api.Orders.CancelAsync(order.Id, Alice, Arg.Any<CancellationToken>()).Returns(Result<Order>.Success(order));

        var response = await _client.PostAsync($"/api/orders/{order.Id}/cancel", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Cancelled", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Cancel_RecordedButNotRunYet_Is202WithTheOrderAndItsLocation()
    {
        var order = NewOrder();
        _api.Orders.CancelAsync(order.Id, Alice, Arg.Any<CancellationToken>()).Returns(Result<Order>.Success(order));

        var response = await _client.PostAsync($"/api/orders/{order.Id}/cancel", content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.EndsWith($"/api/orders/{order.Id}", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cancel_TooLate_Is409Problem()
    {
        var orderId = Guid.CreateVersion7();
        _api.Orders.CancelAsync(orderId, Alice, Arg.Any<CancellationToken>()).Returns(Result<Order>.Failure(OrderErrors.CannotCancel));

        var response = await _client.PostAsync($"/api/orders/{orderId}/cancel", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ordering.cannot_cancel", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Release_HeldOrder_Is202()
    {
        var order = NewOrder();
        _api.Orders.ReleaseAsync(order.Id, Alice, Arg.Any<CancellationToken>()).Returns(Result<Order>.Success(order));

        var response = await _client.PostAsync($"/api/orders/{order.Id}/release", content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Release_OrderNotOnHold_Is409Problem()
    {
        var orderId = Guid.CreateVersion7();
        _api.Orders.ReleaseAsync(orderId, Alice, Arg.Any<CancellationToken>()).Returns(Result<Order>.Failure(OrderErrors.NotOnHold));

        var response = await _client.PostAsync($"/api/orders/{orderId}/release", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ordering.not_on_hold", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task ConfirmDelivery_ShippedOrder_Is202()
    {
        var order = NewOrder();
        _api.Orders.ConfirmDeliveryAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Result<Order>.Success(order));

        var response = await _client.SendAsync(DeliveredByCarrier(order.Id));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmDelivery_ByACustomer_Is403Problem_AndNothingIsConfirmed()
    {
        var orderId = Guid.CreateVersion7();

        var response = await _client.PostAsync($"/api/orders/{orderId}/delivered", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(403, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetInt32());
        await _api.Orders.DidNotReceive().ConfirmDeliveryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmDelivery_NotAwaitingDelivery_Is409Problem()
    {
        var orderId = Guid.CreateVersion7();
        _api.Orders.ConfirmDeliveryAsync(orderId, Arg.Any<CancellationToken>()).Returns(Result<Order>.Failure(OrderErrors.NotAwaitingDelivery));

        var response = await _client.SendAsync(DeliveredByCarrier(orderId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ordering.not_awaiting_delivery", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task ConfirmDelivery_WithoutSignIn_Is401()
    {
        using var anonymous = _api.CreateClient();

        var response = await anonymous.PostAsync($"/api/orders/{Guid.CreateVersion7()}/delivered", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static HttpRequestMessage DeliveredByCarrier(Guid orderId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{orderId}/delivered");
        request.Headers.Add(DemoUserAuthenticationHandler.RoleHeaderName, ApiRoles.Carrier);
        return request;
    }

    public void Dispose()
    {
        _client.Dispose();
        _api.Dispose();
    }

    private void PlaceReturns(Order order, bool pricingFinished) =>
        _api.Orders.PlaceAsync(Arg.Any<PlaceOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<PlacedOrder>.Success(new PlacedOrder(order, pricingFinished)));

    private async Task<HttpResponseMessage> PostWithPreferAsync(string prefer)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(ValidRequest()) };
        request.Headers.TryAddWithoutValidation(PreferWait.HeaderName, prefer);
        return await _client.SendAsync(request);
    }

    private static Order NewOrder() =>
        Order.Create(Alice, Address, "tok_visa", Currency.EUR, [new NewOrderLine(Keyboard, "KEYBOARD-MX", 2, true)]);

    private static object ValidRequest(int quantity = 2, string countryCode = "GR") => new
    {
        lines = new[] { new { productId = Keyboard, quantity } },
        shippingAddress = new { Address.Recipient, Address.Street, Address.City, Address.PostalCode, countryCode },
        paymentToken = "tok_visa",
    };
}
