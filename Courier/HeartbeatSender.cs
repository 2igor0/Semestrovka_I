using System.Net.Sockets;
using Protocol;

namespace Courier;

public static class HeartbeatSender
{
    public static async Task SendAsync(
        Socket socketCourier,
        Guid courierId,
        Guid orderId,
        CancellationToken token)
    {
        var heartbeat = GeneralEncoder.EncodeHeartBeat(courierId, orderId);
        await ProtocolHandler.SendAsync(socketCourier, heartbeat, token);
    }
}
