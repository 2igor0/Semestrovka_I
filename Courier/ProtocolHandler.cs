using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using Protocol;

namespace Courier;

public static class ProtocolHandler
{
    private static IPEndPoint _server = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5050);
    
    public static async Task AcceptUdp(Socket socketCourier, 
        CancellationToken token)
    {
        var remoteIp = new IPEndPoint(IPAddress.Any, 0);
        var bytes = new byte[512];
        
        while (!token.IsCancellationRequested)
        {
            var result = await socketCourier.ReceiveFromAsync(bytes, remoteIp);
            var answer = Encoding.UTF8.GetString(bytes, 0, result.ReceivedBytes); //перекинуть в протокол
            
            //затычка, надо бы полностью как-то разбирать
            answer = answer.Split(" ")[0];
            
            Router(answer, socketCourier);
            
            Console.WriteLine(answer);
            Console.WriteLine(result.RemoteEndPoint);
        }
    }
    
    // наверное надо сделать один протокол для всех
    public static void Router(string message, 
        Socket socketCourier)
    {
        if (message == ProtocolCourier.Order)
        {
            var mesNew = "READY";
            var bytes = Encoding.UTF8.GetBytes(mesNew);

            socketCourier.SendToAsync(bytes, _server);
        }
    }
}