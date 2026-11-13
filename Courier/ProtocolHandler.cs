using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Protocol;
using Protocol.@enum;

namespace Courier;

public static class ProtocolHandler
{
    private static readonly IPEndPoint Server = new(IPAddress.Parse("127.0.0.1"), 5050);
    private static readonly SemaphoreSlim SendGate = new(1, 1);

    public static async Task AcceptUdp(
        Socket socketCourier,
        ChannelWriter<CourierEvent> writer,
        CancellationToken token)
    {
        var remoteIp = new IPEndPoint(IPAddress.Any, 0);
        var bytes = new byte[512];

        while (!token.IsCancellationRequested)
        {
            var result = await socketCourier.ReceiveFromAsync(bytes, remoteIp, token);
            var datagram = bytes.AsSpan(0, result.ReceivedBytes);

            var message = GeneralParser.Pars(datagram);
            if (message is null || !Enum.IsDefined(message.MessageType))
            {
                continue;
            }

            Console.WriteLine($"[UDP] {result.ReceivedBytes} байт, тип {message.MessageType}");
            if (message.MessageType == MessageType.ORDER)
            {
                var order = ProtocolCourier.ParseOrder(message.Payload);

                if (order is null)
                {
                    Console.WriteLine($"Некорректная датаграмма: {result.ReceivedBytes} байт.");
                    continue;
                }

                var ack = GeneralEncoder.EncodeAck(
                    message.CourierId,
                    message.OrderId,
                    MessageType.ORDER);

                await SendAsync(socketCourier, ack, token);
            }
            else if (message.MessageType == MessageType.GO)
            {
                var ack = GeneralEncoder.EncodeAck(
                    message.CourierId,
                    message.OrderId,
                    MessageType.GO);

                await SendAsync(socketCourier, ack, token);
            }

            await writer.WriteAsync(CourierEvent.IncomingMessage(message), token);
        }
    }

    public static async Task SendAsync(Socket socketCourier, byte[] datagram, CancellationToken token)
    {
        await SendGate.WaitAsync(token);

        try
        {
            await socketCourier.SendToAsync(datagram, Server, token);
        }
        finally
        {
            SendGate.Release();
        }
    }
}
