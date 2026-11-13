using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Server.Entities;

namespace Server;

class Program
{
    static async Task Main()
    {
        var socketServer = new Socket(
            AddressFamily.InterNetwork,
            SocketType.Dgram,
            ProtocolType.Udp);

        socketServer.Bind(
            new IPEndPoint(new IPAddress(new byte[] { 127, 0, 0, 1 }), 5050));
        
        var events = Channel.CreateUnbounded<ServerEvent>(); // норм или баундед

        using var cts = new CancellationTokenSource();
        var runtime = new ServerRuntime(socketServer);

        Task generatorTask = Task.Run(
            () => ServerConsole.RunAsync(events.Writer, cts.Token),
            cts.Token);

        Task endpointsTask = Task.Run(
            () => ProtocolHandler.AcceptUdp(socketServer, events.Writer, cts.Token),
            cts.Token);

        Task tickerTask = Task.Run(
            () => Ticker.TimerStart(events.Writer, cts.Token),
            cts.Token);

        Console.WriteLine("Сервер запущен. Ожидание заказов...");

        try
        {
            await runtime.RunAsync(events.Reader, cts.Token);
        }
        finally
        {
            cts.Cancel();
        }

        await Task.WhenAll(generatorTask, endpointsTask, tickerTask);
    }
}
