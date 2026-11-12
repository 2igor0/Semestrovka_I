using Protocol.@enum;

namespace Protocol.Entities;

public record ProtocolMessage(MessageType MessageType, Guid CourierId, Guid OrderId, byte[] Payload );