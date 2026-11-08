using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Protocol;

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
        
        // переделать на Bounded, так как мб перегрузка будет
        var orderChannel = Channel.CreateUnbounded<Order>(); 
        var couriersIp = Channel.CreateUnbounded<Guid>(); 
        
        using var cts = new CancellationTokenSource();

        Task generatorTask = Task.Run(() => StartOrderGeneratorAsync(orderChannel.Writer, cts.Token), cts.Token);
        Task endpointsTask = Task.Run(() => ProtocolHandler.AcceptUdp(socketServer, couriersIp.Writer, cts.Token), cts.Token); 
    
        Console.WriteLine("Сервер запущен. Ожидание заказов...");

        //эта штука зависат, когда нет курьеров и не выводит статусы клиентам, которые сделали новый заказ
        await foreach (Order order in orderChannel.Reader.ReadAllAsync())
        {
            Console.WriteLine($"[Main] Обрабатываю заказ {order.Id}");

            Guid targetCourier;
            if (await couriersIp.Reader.WaitToReadAsync(cts.Token))
            {
                targetCourier = await couriersIp.Reader.ReadAsync(); 
            }
            else
            {
                //тут наверное если такая ситуация, надо будет запускать таску котоая будет ждать появления курьеров
                throw new Exception("нет ни одного курьера на линии, не можем доставить, ваш заказ помещен в очередь");
            }

            var targetEndPointCourier = InMemoryCourierRepository.Get(targetCourier);
            
            //реализовать граматно переброску курьеру
            var bytes = ProtocolServer.EncodeOrder(order, targetCourier);
            await socketServer.SendToAsync(bytes, targetEndPointCourier);
        }

        await Task.WhenAll(generatorTask, endpointsTask);
    }
    
    
    static async Task StartOrderGeneratorAsync(ChannelWriter<Order> writer, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // приходят с сервера заказы продуктов

                Console.WriteLine("Введите адрес для доставки:");
                var address = Console.ReadLine() ?? "";

                Console.WriteLine("Введите продукты для доставки:");
                var description = Console.ReadLine() ?? "";
                
                var order = Order.Create(description, address);
                
                InMemoryPetRepository.Add(order);
                await writer.WriteAsync(order, ct);
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