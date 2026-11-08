namespace Server;

public enum OrderStatus 
{
    New,
    Proposed, //предложенный
    WaitingApprov,
    Confirmed,
    Processing,
    Completed
}