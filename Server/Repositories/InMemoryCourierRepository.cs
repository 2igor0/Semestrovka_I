using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Net;
using Server.Entities;

namespace Server.Repositories;

public class InMemoryCourierRepository
{
    private static ConcurrentDictionary<Guid, CourierInfo> _couriers = new();

    public static void Add(Guid courierKey, CourierInfo courierInfo)
    {
        
        _couriers.TryAdd(courierKey, courierInfo);
    }

    public static bool Contains(Guid courierKey) 
    {
        return _couriers.ContainsKey(courierKey);
    }
    
    public static EndPoint GetEndPointByCourierId(Guid courierKey) 
    {
        return _couriers[courierKey].EndPoint;
    }

    public static CourierInfo GetById(Guid courierId)
    {
        return _couriers[courierId];
    }

    public static ReadOnlyDictionary<Guid, CourierInfo> GetAll()
    {
        return new ReadOnlyDictionary<Guid, CourierInfo>(_couriers);
    }
}