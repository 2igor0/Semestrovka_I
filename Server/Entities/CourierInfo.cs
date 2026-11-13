using System.Net;

namespace Server;

public class CourierInfo(Guid courierId, EndPoint endPoint)
{
    public Guid CourierId { get; set; } = courierId;
    public EndPoint EndPoint { get; set; } = endPoint;
    public DateTime LastSeen { get; set; } = DateTime.Now;
    public Guid CurrentOrderId {get; set;} = Guid.Empty;
    public CourierStatus Status { get; set; } = CourierStatus.IsFree;
}