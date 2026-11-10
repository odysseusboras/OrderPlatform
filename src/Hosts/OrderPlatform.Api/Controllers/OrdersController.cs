using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrderPlatform.Api.Authentication;
using OrderPlatform.Api.Errors;
using OrderPlatform.Api.Models;
using OrderPlatform.Api.Orders;
using OrderPlatform.BuildingBlocks.Paging;
using OrderPlatform.BuildingBlocks.Users;
using OrderPlatform.Ordering.Orders;
using OrderPlatform.Ordering.Placing;

namespace OrderPlatform.Api.Controllers;

/// <summary>The signed-in customer's orders.</summary>
[ApiController]
[Authorize]
[Route("api/orders")]
public sealed class OrdersController(IOrderService orders, ICurrentUser currentUser, IOptions<OrderRequestOptions> options) : ControllerBase
{
    /// <summary>
    /// Places an order. Without a preference the order is accepted (202) and priced in the background.
    /// With <c>Prefer: wait=N</c> the price is calculated within the request, for at most N seconds (capped):
    /// 201 with the priced (or rejected) order, or 202 if it was not ready in time.
    /// With <c>holdBeforePayment</c> the order stops before the payment, stock reserved, until it is released or cancelled.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OrderResponse>> Create(
        CreateOrderRequest request,
        [FromHeader(Name = PreferWait.HeaderName)] string? prefer,
        CancellationToken cancellationToken)
    {
        var wait = PreferWait.Read(prefer, options.Value.MaxWait);
        var result = await orders.PlaceAsync(request.ToCommand(currentUser.UserId, wait), cancellationToken);
        if (!result.IsSuccess)
        {
            return this.ToProblem(result.Error!);
        }

        if (wait is { } applied)
        {
            Response.Headers[PreferWait.AppliedHeaderName] = PreferWait.Applied(applied);
        }

        var placed = result.Value!;
        var body = OrderResponse.From(placed.Order);
        var route = new { orderId = placed.Order.Id };

        return placed.PricingFinished
            ? CreatedAtAction(nameof(Get), route, body)
            : AcceptedAtAction(nameof(Get), route, body);
    }

    /// <summary>Gets one of the customer's orders with its items, price and status history.</summary>
    [HttpGet("{orderId:guid}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> Get(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await orders.GetAsync(orderId, currentUser.UserId, cancellationToken);

        return result.IsSuccess ? Ok(OrderResponse.From(result.Value!)) : this.ToProblem(result.Error!);
    }

    /// <summary>Lists the customer's orders, newest first, one page at a time.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<OrderSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<OrderSummaryResponse>>> List(
        [FromQuery, Range(1, int.MaxValue)] int pageNumber = 1,
        [FromQuery, Range(1, PageRequest.MaxPageSize)] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var page = await orders.ListAsync(currentUser.UserId, new PageRequest(pageNumber, pageSize), cancellationToken);

        return Ok(PagedResponse<OrderSummaryResponse>.From(page.Map(OrderSummaryResponse.From)));
    }

    /// <summary>
    /// Cancels the order: the completed fulfilment steps are undone in reverse order within the request and the
    /// cancelled order is returned (200). 202 when the cancellation is recorded but could not run yet.
    /// A captured payment is refunded and a dispatched parcel is recalled. Allowed until the order is delivered; 409 after that.
    /// </summary>
    [HttpPost("{orderId:guid}/cancel")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> Cancel(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await orders.CancelAsync(orderId, currentUser.UserId, cancellationToken);

        if (!result.IsSuccess)
        {
            return this.ToProblem(result.Error!);
        }

        var body = OrderResponse.From(result.Value!);
        return result.Value!.Status == OrderStatus.Cancelled ? Ok(body) : AcceptedAtAction(nameof(Get), new { orderId }, body);
    }

    /// <summary>
    /// Releases an order that was placed with <c>holdBeforePayment</c> (202): the payment is authorized next and the
    /// fulfilment continues. 409 when the order is not on hold.
    /// </summary>
    [HttpPost("{orderId:guid}/release")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> Release(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await orders.ReleaseAsync(orderId, currentUser.UserId, cancellationToken);

        return result.IsSuccess
            ? AcceptedAtAction(nameof(Get), new { orderId }, OrderResponse.From(result.Value!))
            : this.ToProblem(result.Error!);
    }

    /// <summary>
    /// Confirms that a shipped order was delivered (202); the fulfilment then completes and the order becomes Delivered.
    /// 409 when the order is not waiting for a delivery. This is the carrier's or back-office's call:
    /// it needs the Carrier role (403 without it) and is not limited to the order's owner.
    /// </summary>
    [HttpPost("{orderId:guid}/delivered")]
    [Authorize(Roles = ApiRoles.Carrier)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> ConfirmDelivery(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await orders.ConfirmDeliveryAsync(orderId, cancellationToken);

        return result.IsSuccess
            ? AcceptedAtAction(nameof(Get), new { orderId }, OrderResponse.From(result.Value!))
            : this.ToProblem(result.Error!);
    }
}
