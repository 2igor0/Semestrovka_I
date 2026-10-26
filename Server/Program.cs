using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;


var socketServer = new Socket(
    AddressFamily.InterNetwork, 
    SocketType.Dgram, 
    ProtocolType.Udp);
    
socketServer.Bind(
    new IPEndPoint(new IPAddress(new byte[] { 127, 0, 0, 1 }), 5050));

var remoteIp = new IPEndPoint(IPAddress.Any, 0);
var bytes = new byte[512];
var result = await socketServer.ReceiveFromAsync(bytes, remoteIp);

var answer = Encoding.UTF8.GetString(bytes);
Console.WriteLine(answer);
Console.WriteLine(result.RemoteEndPoint.ToString());