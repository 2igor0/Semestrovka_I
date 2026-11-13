using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Protocol.Entities;
using Protocol.@enum;

namespace Protocol;

public static class ProtocolCourier
{
    private const int LengthPrefixSize = sizeof(ushort);

    private static readonly UTF8Encoding Utf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    
    public static OrderData? ParseOrder(ReadOnlySpan<byte> datagram)
    {
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
        return GeneralEncoder.EncodeMessage(MessageType.READY, courierId, orderId, []);
    }
    
    public static byte[] EncodeStart(Guid courierId)
    {
        return GeneralEncoder.EncodeMessage(MessageType.START, courierId, Guid.Empty, []);
    }
    
    public static byte[] EncodeStarted(Guid courierId, Guid orderId)
    {
        return GeneralEncoder.EncodeMessage(MessageType.STARTED, courierId, orderId, []);
    }
    
    public static byte[] EncodeDelivered(Guid courierId, Guid orderId)
    {
        return GeneralEncoder.EncodeMessage(MessageType.DELIVERED, courierId, orderId, []);
    }

    public static byte[] EncodeCancel(Guid courierId, Guid orderId)
    {
        return GeneralEncoder.EncodeMessage(MessageType.CANCEL, courierId, orderId, []);
    }
}
