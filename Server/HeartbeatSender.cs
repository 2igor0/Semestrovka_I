using System.Net.Sockets;
using Protocol;

namespace Server;

public static class HeartbeatSender
{
    public static async Task SendAsync(
        Socket socketServer,
        IReadOnlyCollection<CourierInfo> busyCouriers,
        CancellationToken token)
    {
        foreach (var courier in busyCouriers)
        {
            var heartbeat = GeneralEncoder.EncodeHeartBeat(
                courier.CourierId,
                courier.CurrentOrderId);

            await socketServer.SendToAsync(heartbeat, courier.EndPoint, token);
        }
    }
}
