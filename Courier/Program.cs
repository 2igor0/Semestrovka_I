using System.Net;
using System.Net.Sockets;
using System.Text;


var socket = new Socket(
    AddressFamily.InterNetwork, 
    SocketType.Dgram, 
    ProtocolType.Udp); 

IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5050);

var message = "Привет, сервер!";
var buffer = Encoding.UTF8.GetBytes(message);

await socket.SendToAsync(buffer, serverEndpoint);




