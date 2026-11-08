using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Protocol;

public static class ProtocolCourier
{
    private const int GuidSize = 16;
    private const int LengthPrefixSize = sizeof(ushort);
    private const int MinDatagramSize = 1 + GuidSize * 2 + LengthPrefixSize * 2; 
    private const int MaxDatagramBytes = 512;

    private static readonly UTF8Encoding Utf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    
    // надо просто парсер сделать и в зависимости от mesType свои обработчик
    public static ProtocolMessage Parser(ReadOnlySpan<byte> datagram)
    {
        if (datagram.Length > MaxDatagramBytes)
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
    
    
    public static OrderData? ParseOrder(ReadOnlySpan<byte> datagram)
    {
        /*if (datagram.Length < MinDatagramSize || datagram.Length > MaxDatagramBytes)
        {
            return null;
        }*/
        
        var source = datagram;
        
        if (!TryReadString(ref source, out var address))
        {
            return null;
        }
        
        if (!TryReadString(ref source, out var description))
        {
            return null;
        }

        if (!source.IsEmpty)
        {
            return null;
        }

        return new OrderData(address, description);
    }

    private static bool TryReadString(
        ref ReadOnlySpan<byte> source,
        [NotNullWhen(true)] out string? value)
    {
        value = null;

        if (source.Length < LengthPrefixSize)
        {
            return false;
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(source[..LengthPrefixSize]); 
        source = source[LengthPrefixSize..];

        if (source.Length < length)
        {
            return false;
        }

        try
        {
            value = Utf8.GetString(source[..length]);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        source = source[length..];
        return true;
    }
    
    public static byte[] EncodeReady(Guid orderId, Guid courierId)
    {
        return ProtocolServer.EncodeMessage(MessageType.READY, courierId, orderId, []);
    }
    
    public static byte[] EncodeStart(Guid courierId)
    {
        return ProtocolServer.EncodeMessage(MessageType.START, courierId, Guid.Empty, []);
    }
}
