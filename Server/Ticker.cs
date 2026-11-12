using System.Threading.Channels;

namespace Server;

public class Ticker
{
    public static async Task TimerStart(ChannelWriter<ServerEvent> writer, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        
        while (await timer.WaitForNextTickAsync(ct))
        {
            await writer.WriteAsync(ServerEvent.Tick(), ct);
        }
    }
}