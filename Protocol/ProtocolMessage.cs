namespace Protocol;

public record ProtocolMessage(MessageType MessageType, Guid CourierId, Guid OrderId, byte[] Payload );