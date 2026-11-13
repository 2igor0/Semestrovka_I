using System.Threading.Channels;

namespace Courier;

public static class Ticker
{
    public static async Task TimerStart(ChannelWriter<CourierEvent> writer, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(ct))
        {
            await writer.WriteAsync(CourierEvent.Tick(), ct);
        }
    }
}
