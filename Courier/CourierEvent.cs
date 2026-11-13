using Protocol.Entities;

namespace Courier;

public enum CourierEventKind
{
    IncomingMessage,
    UserCommand,
    Tick,
    DeliveryReady
}

public sealed class CourierEvent
{
    public CourierEventKind Kind { get; }
    public ProtocolMessage? Message { get; }
    public string? Command { get; }
    public Guid OrderId { get; }
    public int AttemptId { get; }

    private CourierEvent(
        CourierEventKind kind,
        ProtocolMessage? message = null,
        string? command = null,
        Guid orderId = default,
        int attemptId = 0)
    {
        Kind = kind;
        Message = message;
        Command = command;
        OrderId = orderId;
        AttemptId = attemptId;
    }

    public static CourierEvent IncomingMessage(ProtocolMessage message) =>
        new(CourierEventKind.IncomingMessage, message: message);

    public static CourierEvent UserCommand(string command) =>
        new(CourierEventKind.UserCommand, command: command);

    public static CourierEvent Tick() =>
        new(CourierEventKind.Tick);

    public static CourierEvent DeliveryReady(Guid orderId, int attemptId) =>
        new(CourierEventKind.DeliveryReady, orderId: orderId, attemptId: attemptId);
}
