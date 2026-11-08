using System.Net;
using System.Net.Sockets;
using System.Text;
using Protocol;

namespace Courier;

public static class ProtocolHandler
{
    private static readonly IPEndPoint Server = new(IPAddress.Parse("127.0.0.1"), 5050);

    public static async Task AcceptUdp(Socket socketCourier, CancellationToken token)
    {
        var remoteIp = new IPEndPoint(IPAddress.Any, 0);
        var bytes = new byte[512];

        while (!token.IsCancellationRequested)
        {
            var result = await socketCourier.ReceiveFromAsync(bytes, remoteIp, token); //после этого надо тоже парал. таску запускать с дальн. кодом
            var datagram = bytes.AsSpan(0, result.ReceivedBytes);

            var parseAnswer = ProtocolCourier.Parser(datagram);
            if (parseAnswer.MessageType == MessageType.ORDER)
            {
                var order = ProtocolCourier.ParseOrder(parseAnswer.Payload);
                
                if (order is null)
                {
                    Console.WriteLine($"Некорректная датаграмма: {result.ReceivedBytes} байт.");
                    continue;
                }

                var readyBytes = ProtocolCourier.EncodeReady(parseAnswer.OrderId, parseAnswer.CourierId); 
                await socketCourier.SendToAsync(readyBytes, Server, token);
                
                Console.WriteLine($"Получен заказ {parseAnswer.OrderId}.");
                Console.WriteLine($"Адрес: {order.Address}");
                Console.WriteLine($"Описание: {order.Description}");
            }
            else if (parseAnswer.MessageType == MessageType.GO) //еще обработчики
            {
                // ..
            }
        }
    }
}
