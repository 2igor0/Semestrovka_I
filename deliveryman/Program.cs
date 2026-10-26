using System.Net.Sockets;
using System.Text;


var socket = new Socket(
    AddressFamily.InterNetwork, 
    SocketType.Stream, 
    ProtocolType.Tcp); // почему TCP
    
await socket.ConnectAsync("localhost", 5000);

var message = "Привет, сервер!";
var buffer = Encoding.UTF8.GetBytes(message);
await socket.SendAsync(buffer);

byte[] data = new byte[512];

while (true)
{
    int bytes = await socket.ReceiveAsync(data);

    string time = Encoding.UTF8.GetString(data, 0, bytes);
    Console.WriteLine($"Полученное сообщение {time}");
}


