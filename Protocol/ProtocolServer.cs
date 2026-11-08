using System.Buffers.Binary;
using System.Text;

namespace Protocol;

public static class ProtocolServer
{
    public const string Ready = "READY";

    private const int GuidSize = 16;
    private const int LengthPrefixSize = sizeof(ushort);
    private const int MaxDatagramBytes = 512;

    private static readonly UTF8Encoding Utf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    
    public static byte[] EncodeOrder(IOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(order.Address);
        ArgumentNullException.ThrowIfNull(order.Description);

        var addressSize = GetUtf8ByteCount(order.Address, nameof(order.Address));
        var descriptionSize = GetUtf8ByteCount(order.Description, nameof(order.Description));

        var totalSize = LengthPrefixSize + addressSize + 
                           LengthPrefixSize + descriptionSize;

        if (totalSize > MaxDatagramBytes)
        {
            throw new ArgumentException(
                $"Заказ занимает {totalSize} байт, максимум сейчас {MaxDatagramBytes}.",
                nameof(order));
        }

        var buffer = new byte[totalSize];
        var offset = 0;

        BinaryPrimitives.WriteUInt16BigEndian(
            buffer.AsSpan(offset, LengthPrefixSize),
            (ushort)addressSize);
        offset += LengthPrefixSize;

        Utf8.GetBytes(order.Address, buffer.AsSpan(offset, addressSize));
        offset += addressSize;

        BinaryPrimitives.WriteUInt16BigEndian(
            buffer.AsSpan(offset, LengthPrefixSize),
            (ushort)descriptionSize);
        offset += LengthPrefixSize;

        Utf8.GetBytes(order.Description, buffer.AsSpan(offset, descriptionSize));
        
        return EncodeMessage(MessageType.ORDER, Guid.Empty, order.Id, buffer);
    }

    private static int GetUtf8ByteCount(string value, string paramName)
    {
        int size;

        try
        {
            size = Utf8.GetByteCount(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException(
                "Строка содержит некорректную последовательность UTF-16.",
                paramName,
                exception);
        }

        if (size > ushort.MaxValue)
        {
            throw new ArgumentException(
                $"Строка должна занимать не больше {ushort.MaxValue} байт UTF-8.",
                paramName);
        }

        return size;
    }

    private const int HeaderSize = 1 + GuidSize * 2; //33
    
    //перенести
    public static byte[] EncodeMessage(MessageType type, Guid courierId, Guid orderId, byte[] payload)
    {
        // Guid courierId, еще добавить
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
