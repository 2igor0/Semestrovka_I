namespace Protocol;

public class ProtocolServer
{
    public const string Ready = "READY";
    
    public static string ConvertOrder(IOrder order)
    {
        //подумать
        return $"ORDER {order.Id} {order.Address} {order.Description}";
    }
}