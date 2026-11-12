using System.Net.Sockets;

namespace Courier;

public static class Ticker
{
    public static async Task TimerStart(Socket socketCourier, CourierSession session, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(ct))
        {
            foreach (var bytes in session.CollectDue())
            {
                await ProtocolHandler.SendAsync(socketCourier, bytes, ct);
            }
        }
    }
}
