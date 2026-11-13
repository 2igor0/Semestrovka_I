using System.Net;
using Server;
using Server.Entities;
using Server.enums;
using Server.Repositories;

namespace Tests;

public class ServerTests
{
    [Fact]
    public void Silent_busy_courier_returns_order_and_gets_rejected()
    {
        var courierId = Guid.NewGuid();
        var order = Order.Create("Пицца", "Ленина 5");

        order.Status = OrderStatus.Processing;
        order.CourierId = courierId;
        InMemoryOrderRepository.Add(order);

        var courier = new CourierInfo(
            courierId,
            new IPEndPoint(IPAddress.Loopback, 12345))
        {
            Status = CourierStatus.IsBusy,
            CurrentOrderId = order.Id,
            LastSeen = DateTime.Now.AddSeconds(-100)
        };

        InMemoryCourierRepository.Add(courierId, courier);

        var reassigned = CheckerHertbeat.Check();

        Assert.Contains(order.Id, reassigned);
        Assert.Equal(OrderStatus.New, order.Status);
        Assert.Equal(Guid.Empty, order.CourierId);
        Assert.Contains(courierId, order.RejectedBy);
        Assert.Equal(CourierStatus.NotAvailable, courier.Status);
        Assert.Equal(Guid.Empty, courier.CurrentOrderId);
    }

    [Fact]
    public void New_order_has_empty_rejected_set()
    {
        var order = Order.Create("Пицца", "Ленина 5");

        Assert.Empty(order.RejectedBy);
    }
}
