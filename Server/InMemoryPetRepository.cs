using System.Collections.Concurrent;

namespace Server;

public static class InMemoryPetRepository
{
    private static ConcurrentDictionary<Guid, Order> _orders = new();

    public static void Add(Order order)
    {
        _orders.TryAdd(order.Id, order);
    }
    
    public static bool Get(Guid orderId)
    {
        return _orders.ContainsKey(orderId);
    }
}