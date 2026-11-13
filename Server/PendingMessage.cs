using Protocol.@enum;

namespace Server;

internal sealed class PendingMessage
{
    public required byte[] Bytes { get; set; }
    public int Attempts { get; set; }
    public DateTime LastSendTime { get; set; }
}

internal record PendingMessageKey(Guid OrderId, Guid CourierId, MessageType MessageType);
