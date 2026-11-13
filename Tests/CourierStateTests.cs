using Courier;
using Protocol.@enum;

namespace Tests;

public class CourierStateTests
{
    [Fact]
    public void AcceptOrder_stores_order_details()
    {
        var state = new CourierState(Guid.NewGuid());
        var orderId = Guid.NewGuid();

        Assert.True(state.TryAcceptOrder(orderId, "Ленина 5", "Пицца"));
        Assert.Equal(orderId, state.CurrentOrderId);
        Assert.Equal("Ленина 5", state.Address);
        Assert.Equal("Пицца", state.Description);
    }

    [Fact]
    public void Different_order_is_rejected_while_busy()
    {
        var state = new CourierState(Guid.NewGuid());
        var orderId = Guid.NewGuid();

        Assert.True(state.TryAcceptOrder(orderId, "A", "B"));
        Assert.False(state.TryAcceptOrder(Guid.NewGuid(), "C", "D"));
    }

    [Fact]
    public void Repeated_go_does_not_start_second_timer()
    {
        var state = new CourierState(Guid.NewGuid());
        var orderId = Guid.NewGuid();

        state.TryAcceptOrder(orderId, "A", "B");

        Assert.True(state.TryBeginDelivery(orderId, out var attemptId, out var startNewTimer));
        Assert.True(startNewTimer);

        Assert.True(state.TryBeginDelivery(orderId, out _, out var startAnotherTimer));
        Assert.False(startAnotherTimer);
    }

    [Fact]
    public void Done_is_allowed_only_after_delivery_ready()
    {
        var state = new CourierState(Guid.NewGuid());
        var orderId = Guid.NewGuid();

        state.TryAcceptOrder(orderId, "A", "B");
        state.TryBeginDelivery(orderId, out var attemptId, out _);

        Assert.False(state.TryCompleteDelivery(out _));
        Assert.True(state.MarkDeliveryReady(orderId, attemptId));
        Assert.True(state.TryCompleteDelivery(out var completedOrderId));
        Assert.Equal(orderId, completedOrderId);
        Assert.Equal(Guid.Empty, state.CurrentOrderId);
    }

    [Fact]
    public void Revoke_affects_only_current_order()
    {
        var state = new CourierState(Guid.NewGuid());
        var orderId = Guid.NewGuid();

        state.TryAcceptOrder(orderId, "A", "B");
        state.TryBeginDelivery(orderId, out _, out _);

        Assert.False(state.TryRevoke(Guid.NewGuid()));
        Assert.Equal(orderId, state.CurrentOrderId);

        Assert.True(state.TryRevoke(orderId));
        Assert.Equal(Guid.Empty, state.CurrentOrderId);
    }

    [Fact]
    public void Cancel_returns_current_order_and_clears_state()
    {
        var state = new CourierState(Guid.NewGuid());
        var orderId = Guid.NewGuid();

        state.TryAcceptOrder(orderId, "A", "B");

        Assert.True(state.TryCancelCurrent(out var cancelledOrderId));
        Assert.Equal(orderId, cancelledOrderId);
        Assert.Equal(Guid.Empty, state.CurrentOrderId);
    }

    [Fact]
    public void Pending_message_can_be_removed_by_ack()
    {
        var state = new CourierState(Guid.NewGuid());
        var orderId = Guid.NewGuid();

        state.TrackPending(MessageType.READY, orderId, [1, 2, 3]);

        Assert.True(state.TryRemovePending(MessageType.READY, orderId));
        Assert.False(state.TryRemovePending(MessageType.READY, orderId));
    }
}
