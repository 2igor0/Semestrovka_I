using System.Collections.Concurrent;
using Server.Entities;
using Server.enums;

namespace Server.Repositories;

public static class InMemoryOrderRepository
{
    private static ConcurrentDictionary<Guid, Order> _orders = new();

    public static void Add(Order order)
    {
        _orders.TryAdd(order.Id, order);
    }
    
    public static Order? GetById(Guid orderId)
    {
        return _orders.GetValueOrDefault(orderId);
    }

    public static IReadOnlyList<Order> GetNewOrders()
    {
        return _orders.Values
            .Where(order => order.Status == OrderStatus.New)
            .OrderBy(order => order.CreatedAd)
            .ToList();
    }

    public static bool HasActiveOrder(Guid courierId)
    {
        return _orders.Values.Any(order =>
            order.CourierId == courierId &&
            (order.Status == OrderStatus.Proposed ||
             order.Status == OrderStatus.WaitingApprov ||
             order.Status == OrderStatus.Processing));
    }
}