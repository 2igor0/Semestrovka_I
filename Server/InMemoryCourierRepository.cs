using System.Collections.Concurrent;
using System.Net;

namespace Server;

public class InMemoryCourierRepository
{
    private static ConcurrentDictionary<Guid, EndPoint> _couriers = new();

    public static void Add(Guid courierKey, EndPoint endPoint)
    {
        _couriers.TryAdd(courierKey, endPoint);
    }

    public static void Update(Guid courierId, EndPoint endPoint)
    {
        _couriers[courierId] = endPoint;
    }

    public static bool Contains(Guid courierKey) 
    {
        return _couriers.ContainsKey(courierKey);
    }
    
    public static EndPoint Get(Guid courierKey) 
    {
        return _couriers[courierKey];
    }
}