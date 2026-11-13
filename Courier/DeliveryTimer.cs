using System.Threading.Channels;

namespace Courier;

public static class DeliveryTimer
{
    public static async Task RunAsync(
        ChannelWriter<CourierEvent> writer,
        Guid orderId,
        int attemptId,
        CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(CourierConstants.DeliverySeconds), token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await writer.WriteAsync(CourierEvent.DeliveryReady(orderId, attemptId), token);
    }
}
