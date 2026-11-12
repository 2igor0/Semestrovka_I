using System.Text;
using Protocol.@enum;

namespace Protocol;

public static class GeneralEncoder
{
    private const int GuidSize = 16;
    private const int MaxDatagramBytes = 512;
    
    private static readonly UTF8Encoding Utf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    
    private const int HeaderSize = 1 + GuidSize * 2; //33
    
    public static byte[] EncodeMessage(MessageType type, Guid courierId, Guid orderId, byte[] payload)
    {
        var totalSize = 1 + GuidSize * 2 + payload.Length; // 1 это type + 16 * 2 это guid
        var buffer = new byte[totalSize];
        
        if (totalSize > MaxDatagramBytes)
        {
            throw new ArgumentException(
                $"Заказ занимает {totalSize} байт, максимум сейчас {MaxDatagramBytes}.");
        }

        buffer[0] = (byte)type;
        courierId.TryWriteBytes(buffer.AsSpan(1, GuidSize));
        orderId.TryWriteBytes(buffer.AsSpan(17, GuidSize));
        payload.CopyTo(buffer.AsSpan(HeaderSize));

        return buffer;
    }
}