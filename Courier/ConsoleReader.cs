using System.Threading.Channels;

namespace Courier;

public static class ConsoleReader
{
    public static async Task RunAsync(ChannelWriter<CourierEvent> writer, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var line = Console.ReadLine();

            if (line is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            await writer.WriteAsync(CourierEvent.UserCommand(line.Trim()), token);
        }
    }
}
