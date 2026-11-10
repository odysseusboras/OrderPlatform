using OrderPlatform.Ordering.Fulfilment.Steps;
using OrderPlatform.Ordering.Orders;
using OrderPlatform.ProcessEngine.Runs;

namespace OrderPlatform.Ordering.Fulfilment;

/// <summary>The order's status as a function of its fulfilment run. Pure rules, so they are easy to test.</summary>
public static class OrderProgress
{
    /// <summary>The status an order reaches once a step has passed (succeeded or skipped), in pipeline order.</summary>
    private static readonly (string StepKey, OrderStatus Status)[] Milestones =
    [
        (Step03AuthorizePayment.Key, OrderStatus.Confirmed),
        (Step05CapturePayment.Key, OrderStatus.Paid),
        (Step06DispatchShipment.Key, OrderStatus.Shipped),
    ];

    /// <summary>
    /// Brings the order in line with the run: Priced once the price exists; Confirmed, Paid and Shipped as their steps pass;
    /// Delivered when the run completes; Cancelled, Rejected or Failed as soon as the run starts undoing
    /// (the decision is made; releasing what was held is the run's job). Safe to call after every move.
    /// </summary>
    public static void Apply(
        Order order, RunStatus runStatus, CompensationCause? cause, string? reason, PriceSnapshot? price, IReadOnlySet<string>? passedSteps = null)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (price is not null && order.Status == OrderStatus.Pending)
        {
            order.ApplyPrice(price.Subtotal, price.Tax, price.ShippingCharge, price.Total);
        }

        foreach (var (stepKey, status) in Milestones)
        {
            if (passedSteps?.Contains(stepKey) == true && order.CanTransitionTo(status))
            {
                order.TransitionTo(status);
            }
        }

        if (runStatus == RunStatus.Completed && order.CanTransitionTo(OrderStatus.Delivered))
        {
            order.TransitionTo(OrderStatus.Delivered);
        }

        if (runStatus is not (RunStatus.Compensating or RunStatus.Compensated))
        {
            return;
        }

        var next = cause == CompensationCause.Cancelled ? OrderStatus.Cancelled
            : order.CanTransitionTo(OrderStatus.Rejected) ? OrderStatus.Rejected
            : OrderStatus.Failed;

        if (order.CanTransitionTo(next))
        {
            order.TransitionTo(next, reason);
        }
    }
}
