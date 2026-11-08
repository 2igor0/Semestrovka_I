namespace Protocol;

public enum MessageType: byte
{
    START,
    ORDER,
    ACK,
    READY,
    GO,
    STARTED,
    DELIVERED
}