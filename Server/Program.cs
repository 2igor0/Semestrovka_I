using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Protocol;
using Protocol.Entities;
using Protocol.@enum;

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
        var events = Channel.CreateUnbounded<ServerEvent>();

        var availableCouriers = new Queue<Guid>();
        var availableCourierKeys = new HashSet<Guid>();
        
        using var cts = new CancellationTokenSource();

        Task generatorTask = Task.Run(() => StartOrderGeneratorAsync(events.Writer, cts.Token), cts.Token);
        Task endpointsTask = Task.Run(() => ProtocolHandler.AcceptUdp(socketServer, events.Writer, cts.Token), cts.Token); 
    
        Console.WriteLine("Сервер запущен. Ожидание заказов...");

        try
        {
            await foreach (var serverEvent in events.Reader.ReadAllAsync(cts.Token))
            {
                switch (serverEvent.Kind)
                {
                    case ServerEventKind.NewOrder:
                        InMemoryOrderRepository.Add(serverEvent.Order!);
                        Console.WriteLine($"[Main] Обрабатываю заказ {serverEvent.Order!.Id}");
                        break;

                    case ServerEventKind.IncomingMessage:
                        await HandleIncomingMessageAsync(
                            serverEvent,
                            socketServer,
                            availableCouriers,
                            availableCourierKeys,
                            cts.Token);
                        break;

                    case ServerEventKind.Tick:
                        break;
                }

                await TryDispatchOrdersAsync(
                    socketServer,
                    availableCouriers,
                    availableCourierKeys,
                    cts.Token);
            }
        }
        finally
        {
            cts.Cancel();
        }

        await Task.WhenAll(generatorTask, endpointsTask);
    }
    
    
    static async Task StartOrderGeneratorAsync(ChannelWriter<ServerEvent> writer, CancellationToken ct)
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
                
                await writer.WriteAsync(ServerEvent.NewOrder(order), ct);
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

    private static void MarkCourierAvailable(
        Guid courierKey,
        Queue<Guid> availableCouriers,
        HashSet<Guid> availableCourierKeys)
    {
        if (availableCourierKeys.Add(courierKey))
        {
            availableCouriers.Enqueue(courierKey);
            Console.WriteLine($"[Main] Курьер {courierKey} свободен.");
        }
    }

    private static async Task HandleIncomingMessageAsync(
        ServerEvent serverEvent,
        Socket socketServer,
        Queue<Guid> availableCouriers,
        HashSet<Guid> availableCourierKeys,
        CancellationToken token)
    {
        var message = serverEvent.Message!;
        var remoteEndPoint = serverEvent.Remote!;

        if (message.MessageType == MessageType.START)
        {
            if (InMemoryCourierRepository.Contains(message.CourierId)) // надо еще проверять есть ли у него текущие заказы
            {
                InMemoryCourierRepository.Update(message.CourierId, remoteEndPoint);
            }
            else
            {
                InMemoryCourierRepository.Add(message.CourierId, remoteEndPoint);
            }

            if (!availableCourierKeys.Contains(message.CourierId) &&
                !InMemoryOrderRepository.HasActiveOrder(message.CourierId))
            {
                MarkCourierAvailable(message.CourierId, availableCouriers, availableCourierKeys);
            }
        }
        else if (message.MessageType == MessageType.READY)
        {
            var order = InMemoryOrderRepository.GetById(message.OrderId);
            
            if (order is null)
            {
                await Revoked(message, remoteEndPoint, socketServer, token);
                return;
            }
            
            if (order.CourierId != message.CourierId)
            {
                await Revoked(message, remoteEndPoint, socketServer, token);
                Console.WriteLine("Заказ уже сменил курьера!"); 
                return;
            }
            else if (order.CourierId == message.CourierId)
            {
                var mes = ProtocolServer.EncodeGo(message.OrderId, message.CourierId);
                
                if (order.Status == OrderStatus.WaitingApprov)
                    await socketServer.SendToAsync(mes, remoteEndPoint, token);
                else if (order.Status == OrderStatus.Proposed)
                {
                    await socketServer.SendToAsync(mes, remoteEndPoint, token);
                    order.Status = OrderStatus.WaitingApprov;
                }
                else
                {
                    Console.WriteLine("Че то намудрили, ничего не произошло!");
                    return;
                }
            }
            else
            {
                Console.WriteLine("Че то намудрили, ничего не произошло!");
                return;
            }
        }
        else if (message.MessageType == MessageType.ACK)
        {
            
        }
        else if (message.MessageType == MessageType.STARTED)
        {
            var order = InMemoryOrderRepository.GetById(message.OrderId);

            if (order is null)
            {
                await Revoked(message, remoteEndPoint, socketServer, token);
                return;
            }

            if (order.CourierId != message.CourierId)
            {
                await Revoked(message, remoteEndPoint, socketServer, token);
                return;
            }

            if (order.Status  == OrderStatus.WaitingApprov)
                order.Status = OrderStatus.Processing;
            else return;
        }
        else if (message.MessageType == MessageType.DELIVERED)
        {
            var order = InMemoryOrderRepository.GetById(message.OrderId);
            
            if (order is null)
            {
                await Revoked(message, remoteEndPoint, socketServer, token);
                return;
            }
            
            if (order.CourierId != message.CourierId)
            {
                await Revoked(message, remoteEndPoint, socketServer, token);
                return;
            }

            if (order.Status == OrderStatus.Processing)
            {
                order.Status = OrderStatus.Completed;
                MarkCourierAvailable(order.CourierId, availableCouriers, availableCourierKeys);
            }
            else return;
        }
        else
        {
            Console.WriteLine($"Неизвестный тип сообщения: {message.MessageType}");
        }

        Console.WriteLine($"уже обработали {message.MessageType}");
    }

    private static async Task Revoked(ProtocolMessage messsage, EndPoint endPoint, Socket socketServer, CancellationToken token)
    {
        var bytes = ProtocolServer.EncodeRevoked(messsage.OrderId, messsage.CourierId);
        await socketServer.SendToAsync(bytes, endPoint, token);
    }

    private static async Task TryDispatchOrdersAsync(
        Socket socketServer,
        Queue<Guid> availableCouriers,
        HashSet<Guid> availableCourierKeys,
        CancellationToken token)
    {
        var newOrders = InMemoryOrderRepository.GetNewOrders().ToList();

        while (availableCouriers.Count > 0 && newOrders.Count > 0)
        {
            var courierKey = availableCouriers.Dequeue();
            availableCourierKeys.Remove(courierKey);

            if (!InMemoryCourierRepository.Contains(courierKey))
            {
                continue;
            }

            var courierEndPoint = InMemoryCourierRepository.Get(courierKey);
            var order = newOrders[0];
            newOrders.RemoveAt(0);
            
            var bytes = ProtocolServer.EncodeOrder(order, courierKey);
            await socketServer.SendToAsync(bytes, courierEndPoint, token);

            order.CourierId = courierKey;
            order.Status = OrderStatus.Proposed;
            order.Attempts++;
            order.LastSentAt = Environment.TickCount64;

            Console.WriteLine($"[Main] Заказ {order.Id} отправлен курьеру {courierKey}.");
        }
    }
}