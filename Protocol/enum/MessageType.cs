namespace Protocol.@enum;

public enum MessageType: byte
{
    START,
    ORDER,
    ACK,
    READY,
    GO,
    STARTED,
    DELIVERED,
    REVOKED
}