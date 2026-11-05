using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using Protocol;

namespace Server;

public static class ProtocolHandler
{
    public static async Task AcceptUdp(Socket socketServer, ChannelWriter<EndPoint> endPointWriter, CancellationToken token)
    {
        var remoteIp = new IPEndPoint(IPAddress.Any, 0);
        var bytes = new byte[512];
        
        while (!token.IsCancellationRequested)
        {
            var result = await socketServer.ReceiveFromAsync(bytes, remoteIp);
            
            var answer = Encoding.UTF8.GetString(bytes, 0, result.ReceivedBytes);
            
            if (answer == "start")
            {
                await endPointWriter.WriteAsync(result.RemoteEndPoint, token);
            }
            else
            {
                Router(answer, result.RemoteEndPoint);
            }
            
            Console.WriteLine(answer);
            Console.WriteLine(result.RemoteEndPoint);
        }
    }
    
    public static void Router(string message, EndPoint endPoint)
    {
        if (message == ProtocolServer.Ready)
        {
            Console.WriteLine("курьер ответил готов");
        }
    }
    
    
}