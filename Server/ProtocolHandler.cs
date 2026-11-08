using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using Protocol;

namespace Server;

public static class ProtocolHandler //всю архитектуру пересотреть везде
{
    public static async Task AcceptUdp(Socket socketServer, ChannelWriter<Guid> endPointWriter, CancellationToken token)
    {
        var remoteIp = new IPEndPoint(IPAddress.Any, 0);
        var bytes = new byte[512];
        
        while (!token.IsCancellationRequested)
        {
            var result = await socketServer.ReceiveFromAsync(bytes, remoteIp); //после этого еще одну таску запускать
            var datagram = bytes.AsSpan(0, result.ReceivedBytes);
            var answer = ProtocolCourier.Parser(datagram);
            
            if (answer.MessageType == MessageType.START)
            {
                if (InMemoryCourierRepository.Contains(answer.CourierId)) // надо еще проверять есть ли у него текущие заказы
                {
                    InMemoryCourierRepository.Update(answer.CourierId, result.RemoteEndPoint);
                }
                else
                {
                    InMemoryCourierRepository.Add(answer.CourierId, result.RemoteEndPoint);
                }
               
                await endPointWriter.WriteAsync(answer.CourierId, token);
            }
            else if (answer.MessageType == MessageType.READY)
            {
                if (InMemoryPetRepository.Get(answer.OrderId)) // так же проверять прошлый статус
                {
                    Console.WriteLine($"READY курьер ответил готов, на вот этот СУЩЕСТВУЮЩИЙ заказ: {answer.OrderId}");
                }
                else
                {
                    Console.WriteLine($"READY курьер ответил готов, на несуществующий заказ: {answer.OrderId}");
                }
            }
            else if (answer.MessageType == MessageType.ACK)
            {
                
            }
            else if (answer.MessageType == MessageType.STARTED)
            {
                
            }
            else 
            {
                throw new Exception($"Неопределенный ввод {answer.MessageType}");
            }
            
            Console.WriteLine($"уже обработали {answer.MessageType}");
        }
    }
}