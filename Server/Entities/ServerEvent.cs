using System.Net;
using Protocol;
using Protocol.Entities;

namespace Server;

public enum ServerEventKind
{
    NewOrder,
    IncomingMessage,
    Tick
}

public sealed class ServerEvent
{
    public ServerEventKind Kind { get; }
    public Order? Order { get; }
    public Guid? CourierKey { get; }
    public ProtocolMessage? Message { get; }
    public EndPoint? Remote { get; }

    private ServerEvent(
        ServerEventKind kind,
        Order? order = null,
        Guid? courierKey = null,
        ProtocolMessage? message = null,
        EndPoint? remote = null)
    {
        Kind = kind;
        Order = order;
        CourierKey = courierKey;
        Message = message;
        Remote = remote;
    }

    public static ServerEvent NewOrder(Order order) =>
        new(ServerEventKind.NewOrder, order);

    public static ServerEvent IncomingMessage(ProtocolMessage message, EndPoint remote) =>
        new(ServerEventKind.IncomingMessage, null, null, message, remote);

    public static ServerEvent Tick() =>
        new(ServerEventKind.Tick);
}
