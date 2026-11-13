using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using Protocol;
using Protocol.@enum;
using Server.Entities;

namespace Server;

public static class ProtocolHandler 
{
    public static async Task AcceptUdp(Socket socketServer, ChannelWriter<ServerEvent> eventWriter, CancellationToken token)
    {
        var remoteIp = new IPEndPoint(IPAddress.Any, 0);
        var bytes = new byte[512];
        
        while (!token.IsCancellationRequested)
        {
            var result = await socketServer.ReceiveFromAsync(bytes, remoteIp, token);
            var datagram = bytes.AsSpan(0, result.ReceivedBytes);
            var answer = GeneralParser.Pars(datagram);

            if (!Enum.IsDefined(answer.MessageType))
            {
                continue;
            }

            if (answer.MessageType != MessageType.ACK
                && answer.MessageType != MessageType.HEARTBEAT
                && answer.MessageType != MessageType.REVOKED)
            {
                var ackBytes = GeneralEncoder.EncodeAck(answer.CourierId, answer.OrderId, answer.MessageType);
                await socketServer.SendToAsync(ackBytes, result.RemoteEndPoint, token);
            }

            await eventWriter.WriteAsync(
                ServerEvent.IncomingMessage(answer, result.RemoteEndPoint),
                token);
        }
    }
}