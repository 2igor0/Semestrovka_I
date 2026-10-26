using System.Net;
using System.Net.Sockets;
using System.Text;


var socketServer = new Socket(
    AddressFamily.InterNetwork, 
    SocketType.Stream, 
    ProtocolType.Tcp);
    
socketServer.Bind(
    new IPEndPoint(new IPAddress(new byte[] { 127, 0, 0, 1 }), 5000));
    
socketServer.Listen();

var clients = new List<Socket>(); // сделать очередь, сделать блокировку потока, чтобы убирать из начала и добавлять в конец
Console.WriteLine("Сервер запущен. Ожидание подключений...");

Task.Run(async () =>
{
    while (true)
    {
        Socket client = await socketServer.AcceptAsync(); //короче из-за using у нас походу удаляется объект!
        Console.WriteLine($"Адрес подключенного клиента: {client.RemoteEndPoint}");
        clients.Add(client);
    }
});


byte[] data = new byte[512];

while (true)
{
    if (clients.Count != 0)
    {
        var receive = await clients[0].ReceiveAsync(data);
        Console.WriteLine(Encoding.UTF8.GetString(data, 0 , receive));
    }
} 
