using System.Net;
using System.Net.Sockets;
using System.Text;
using Courier;
using Protocol;

var socket = new Socket(
    AddressFamily.InterNetwork, 
    SocketType.Dgram, 
    ProtocolType.Udp); 

IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5050);

var profile = "courier1"; 
var path = Path.Combine("profiles", $"{profile}.key");
var text = File.ReadAllText(path).Trim();

if (!Guid.TryParse(text, out var courierKey))
{
    Console.WriteLine($"В файле {path} нет корректного GUID.");
    return;
}

Console.WriteLine("Для выхода на линию введи start: ");
var command = Console.ReadLine();
while (command != "start")
{
    Console.WriteLine("Для выхода на линию все-таки введите start: ");
    command = Console.ReadLine();
}

var buffer = ProtocolCourier.EncodeStart(courierKey);

await socket.SendToAsync(buffer, serverEndpoint);

await Task.Run(() => ProtocolHandler.AcceptUdp(socket, CancellationToken.None));
