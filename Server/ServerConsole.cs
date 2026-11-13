using System.Threading.Channels;

namespace Server;

public static class ServerConsole
{
    public static async Task RunAsync(ChannelWriter<ServerEvent> writer, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                Console.WriteLine("Введите адрес для доставки:");
                var address = Console.ReadLine() ?? "";

                Console.WriteLine("Введите продукты для доставки:");
                var description = Console.ReadLine() ?? "";

                var order = Order.Create(description, address);

                await writer.WriteAsync(ServerEvent.NewOrder(order), token);
                Console.WriteLine($"-> [Фон] Появился новый заказ # {order.Id}");
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            writer.Complete();
        }
    }
}
