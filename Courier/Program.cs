using System.Net;
using System.Net.Sockets;
using System.Text;
using Courier;

var socket = new Socket(
    AddressFamily.InterNetwork, 
    SocketType.Dgram, 
    ProtocolType.Udp); 

IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5050);
Console.WriteLine("Для выхода на линию введи start: ");
var command = Console.ReadLine();

var buffer = Encoding.UTF8.GetBytes(command);

/*Console.WriteLine($"[{command}] длина: {command.Length}");
Console.WriteLine(string.Join(" ", buffer.Take(20)));*/

await socket.SendToAsync(buffer, serverEndpoint);

await Task.Run(() => ProtocolHandler.AcceptUdp(socket, CancellationToken.None));

/*await socket.ReceiveFromAsync(buffer, serverEndpoint);

var answer = Encoding.UTF8.GetString(buffer);
Console.WriteLine(answer);*/