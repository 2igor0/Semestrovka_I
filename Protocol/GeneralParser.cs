using Protocol.Entities;
using Protocol.@enum;

namespace Protocol;

public class GeneralParser
{
    private const int GuidSize = 16;
    private const int MinDatagramSize = 1 + GuidSize * 2;
    private const int MaxDatagramBytes = 512;
    
    public static ProtocolMessage Pars(ReadOnlySpan<byte> datagram)
    {
        if (datagram.Length < MinDatagramSize || datagram.Length > MaxDatagramBytes) 
        {
            return null;
        }
        
        var source = datagram;
        var mesType = source[0];
        source = source[1..];
        
        var courierId = new Guid(source[..GuidSize]);
        source = source[GuidSize..];
        
        var orderId = new Guid(source[..GuidSize]);
        source = source[GuidSize..];

        return new ProtocolMessage((MessageType)mesType, courierId, orderId, source.ToArray());
    }

    public static bool TryParseAckedType(byte[] payload, out MessageType ackedType)
    {
        ackedType = default;

        if (payload is null || payload.Length != 1)
        {
            return false;
        }

        if (!Enum.IsDefined(typeof(MessageType), payload[0]))
        {
            return false;
        }

        ackedType = (MessageType)payload[0];
        return true;
    }
}