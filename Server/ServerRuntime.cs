using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Protocol;
using Protocol.Entities;
using Protocol.@enum;

namespace Server;

public sealed class ServerRuntime
{
    private readonly Socket _socketServer;
    private readonly List<Guid> _availableCouriers = new();
    private readonly HashSet<Guid> _availableCourierKeys = new();
    private readonly Dictionary<PendingMessageKey, PendingMessage> _pendingMessages = new();

    public ServerRuntime(Socket socketServer)
    {
        _socketServer = socketServer;
    }

    public async Task RunAsync(ChannelReader<ServerEvent> reader, CancellationToken token)
    {
        await foreach (var serverEvent in reader.ReadAllAsync(token))
        {
            switch (serverEvent.Kind)
            {
                case ServerEventKind.NewOrder:
                    InMemoryOrderRepository.Add(serverEvent.Order!);
                    Console.WriteLine($"[Main] Обрабатываю заказ {serverEvent.Order!.Id}");
                    break;

                case ServerEventKind.IncomingMessage:
                    await HandleIncomingMessageAsync(serverEvent, token);
                    break;

                case ServerEventKind.Tick:
                    await HandleTickAsync(token);
                    break;
            }

            await TryDispatchOrdersAsync(token);
        }
    }

    private void MarkCourierAvailable(Guid courierKey)
    {
        if (_availableCourierKeys.Add(courierKey))
        {
            _availableCouriers.Add(courierKey);
            Console.WriteLine($"[Main] Курьер {courierKey} свободен.");
        }
    }

    private async Task HandleIncomingMessageAsync(ServerEvent serverEvent, CancellationToken token)
    {
        var message = serverEvent.Message!;
        var remoteEndPoint = serverEvent.Remote!;

        if (InMemoryCourierRepository.Contains(message.CourierId))
        {
            var courierInfo = InMemoryCourierRepository.GetById(message.CourierId);
            courierInfo.LastSeen = DateTime.Now;
            courierInfo.EndPoint = remoteEndPoint;

            if (courierInfo.Status == CourierStatus.NotAvailable
                && !InMemoryOrderRepository.HasActiveOrder(message.CourierId))
            {
                courierInfo.Status = CourierStatus.IsFree;

                if (!_availableCourierKeys.Contains(message.CourierId))
                {
                    MarkCourierAvailable(message.CourierId);
                }
            }
        }

        if (message.MessageType == MessageType.START)
        {
            var hasActiveOrder = InMemoryOrderRepository.HasActiveOrder(message.CourierId);

            if (InMemoryCourierRepository.Contains(message.CourierId))
            {
                var courierInfo = InMemoryCourierRepository.GetById(message.CourierId);
                courierInfo.EndPoint = remoteEndPoint;

                if (!hasActiveOrder)
                {
                    courierInfo.Status = CourierStatus.IsFree;
                }
            }
            else
            {
                InMemoryCourierRepository.Add(
                    message.CourierId,
                    new CourierInfo(message.CourierId, remoteEndPoint));
            }

            if (!_availableCourierKeys.Contains(message.CourierId) && !hasActiveOrder)
            {
                MarkCourierAvailable(message.CourierId);
            }
        }
        else if (message.MessageType == MessageType.READY)
        {
            var order = InMemoryOrderRepository.GetById(message.OrderId);

            if (order is null)
            {
                await Revoked(message, remoteEndPoint, token);
                return;
            }

            if (order.CourierId != message.CourierId)
            {
                await Revoked(message, remoteEndPoint, token);
                Console.WriteLine("Заказ уже сменил курьера!");
                return;
            }

            var mes = ProtocolServer.EncodeGo(message.OrderId, message.CourierId);
            _pendingMessages.Remove(new PendingMessageKey(order.Id, message.CourierId, MessageType.ORDER));

            if (order.Status == OrderStatus.WaitingApprov)
            {
                var key = new PendingMessageKey(order.Id, message.CourierId, MessageType.GO);
                if (_pendingMessages.TryGetValue(key, out var pending))
                {
                    pending.Attempts++;
                    pending.LastSendTime = DateTime.Now;
                }

                await _socketServer.SendToAsync(mes, remoteEndPoint, token);
            }
            else if (order.Status == OrderStatus.Proposed)
            {
                var key = new PendingMessageKey(order.Id, message.CourierId, MessageType.GO);
                var value = new PendingMessage
                {
                    Bytes = mes,
                    Attempts = 0,
                    LastSendTime = DateTime.Now
                };
                _pendingMessages.Add(key, value);

                await _socketServer.SendToAsync(mes, remoteEndPoint, token);
                order.Status = OrderStatus.WaitingApprov;
            }
            else
            {
                Console.WriteLine("Че то намудрили, ничего не произошло!");
                return;
            }
        }
        else if (message.MessageType == MessageType.ACK)
        {
            if (GeneralParser.TryParseAckedType(message.Payload, out var ackedType))
            {
                var key = new PendingMessageKey(message.OrderId, message.CourierId, ackedType);
                _pendingMessages.Remove(key);
            }
        }
        else if (message.MessageType == MessageType.STARTED)
        {
            var order = InMemoryOrderRepository.GetById(message.OrderId);

            if (order is null || order.CourierId != message.CourierId)
            {
                await Revoked(message, remoteEndPoint, token);
                return;
            }

            if (order.Status == OrderStatus.WaitingApprov)
            {
                var key = new PendingMessageKey(order.Id, message.CourierId, MessageType.GO);
                _pendingMessages.Remove(key);
                order.Status = OrderStatus.Processing;
            }
            else
            {
                return;
            }
        }
        else if (message.MessageType == MessageType.DELIVERED)
        {
            var order = InMemoryOrderRepository.GetById(message.OrderId);

            if (order is null)
            {
                await Revoked(message, remoteEndPoint, token);
                return;
            }

            if (order.CourierId != message.CourierId)
            {
                await Revoked(message, remoteEndPoint, token);
                return;
            }

            if (order.Status != OrderStatus.Processing)
            {
                return;
            }

            order.Status = OrderStatus.Completed;

            var courierInfo = InMemoryCourierRepository.GetById(message.CourierId);
            courierInfo.Status = CourierStatus.IsFree;
            courierInfo.CurrentOrderId = Guid.Empty;

            MarkCourierAvailable(order.CourierId);
        }
        else if (message.MessageType == MessageType.CANCEL)
        {
            var order = InMemoryOrderRepository.GetById(message.OrderId);

            if (order is null)
            {
                return;
            }

            if (order.CourierId != message.CourierId)
            {
                await Revoked(message, remoteEndPoint, token);
                return;
            }

            if (order.Status == OrderStatus.New || order.Status == OrderStatus.Completed)
            {
                return;
            }

            order.Status = OrderStatus.New;
            order.CourierId = Guid.Empty;
            order.RejectedBy.Add(message.CourierId);

            foreach (var key in _pendingMessages.Keys
                         .Where(key => key.OrderId == message.OrderId)
                         .ToList())
            {
                _pendingMessages.Remove(key);
            }

            if (InMemoryCourierRepository.Contains(message.CourierId))
            {
                var courierInfo = InMemoryCourierRepository.GetById(message.CourierId);
                courierInfo.CurrentOrderId = Guid.Empty;
                courierInfo.Status = CourierStatus.IsFree;

                if (!_availableCourierKeys.Contains(message.CourierId))
                {
                    MarkCourierAvailable(message.CourierId);
                }
            }

            Console.WriteLine($"Заказ {message.OrderId} отменён курьером {message.CourierId}.");
        }
        else if (message.MessageType == MessageType.HEARTBEAT)
        {
            if (!InMemoryCourierRepository.Contains(message.CourierId))
            {
                return;
            }

            var courierInfo = InMemoryCourierRepository.GetById(message.CourierId);

            if (courierInfo.Status != CourierStatus.IsFree)
            {
                var order = InMemoryOrderRepository.GetById(message.OrderId);

                if (order is null || order.CourierId != message.CourierId)
                {
                    await Revoked(message, remoteEndPoint, token);
                    Console.WriteLine("Не нашли нужный заказ, либо уже переназначили курьера");
                }
            }
        }
        else
        {
            Console.WriteLine($"Неизвестный тип сообщения: {message.MessageType}");
        }

        Console.WriteLine($"уже обработали {message.MessageType}");
    }

    private async Task Revoked(ProtocolMessage message, EndPoint endPoint, CancellationToken token)
    {
        var bytes = ProtocolServer.EncodeRevoked(message.OrderId, message.CourierId);
        await _socketServer.SendToAsync(bytes, endPoint, token);
    }

    private static bool IsHeartbeatPhase(Guid orderId)
    {
        if (orderId == Guid.Empty)
        {
            return false;
        }

        var order = InMemoryOrderRepository.GetById(orderId);

        return order is not null
               && (order.Status == OrderStatus.WaitingApprov
                   || order.Status == OrderStatus.Processing);
    }

    private async Task HandleTickAsync(CancellationToken token)
    {
        var reassignedOrderIds = CheckerHertbeat.Check();

        foreach (var orderId in reassignedOrderIds)
        {
            foreach (var key in _pendingMessages.Keys
                         .Where(key => key.OrderId == orderId)
                         .ToList())
            {
                _pendingMessages.Remove(key);
            }

            Console.WriteLine($"Заказ {orderId} возвращён в New: курьер не отвечает.");
        }

        var busyCouriers = InMemoryCourierRepository.GetAll().Values
            .Where(courier => courier.Status == CourierStatus.IsBusy
                              && IsHeartbeatPhase(courier.CurrentOrderId))
            .ToList();

        await HeartbeatSender.SendAsync(_socketServer, busyCouriers, token);

        foreach (var (key, value) in _pendingMessages.ToList())
        {
            if ((DateTime.Now - value.LastSendTime).TotalSeconds < ServerConstants.ResendIntervalSeconds)
            {
                continue;
            }

            if (value.Attempts >= ServerConstants.MaxResendAttempts)
            {
                _pendingMessages.Remove(key);

                if (key.MessageType == MessageType.ORDER)
                {
                    var order = InMemoryOrderRepository.GetById(key.OrderId);

                    if (order is not null && order.Status == OrderStatus.Proposed)
                    {
                        order.Status = OrderStatus.New;
                        order.CourierId = Guid.Empty;
                        order.RejectedBy.Add(key.CourierId);
                    }

                    if (InMemoryCourierRepository.Contains(key.CourierId))
                    {
                        var courier = InMemoryCourierRepository.GetById(key.CourierId);

                        if (courier.CurrentOrderId == key.OrderId)
                        {
                            courier.CurrentOrderId = Guid.Empty;
                            courier.Status = CourierStatus.NotAvailable;
                        }
                    }

                    Console.WriteLine(
                        $"Исчерпаны повторы ORDER по заказу {key.OrderId}. Заказ возвращён в New.");
                }
                else
                {
                    Console.WriteLine($"Исчерпаны повторы для {key.MessageType} по заказу {key.OrderId}.");
                }

                continue;
            }

            if (!InMemoryCourierRepository.Contains(key.CourierId))
            {
                _pendingMessages.Remove(key);
                continue;
            }

            var courierEndPoint = InMemoryCourierRepository.GetEndPointByCourierId(key.CourierId);
            await _socketServer.SendToAsync(value.Bytes, courierEndPoint, token);

            value.Attempts++;
            value.LastSendTime = DateTime.Now;
        }
    }

    private async Task TryDispatchOrdersAsync(CancellationToken token)
    {
        _availableCouriers.RemoveAll(courierKey =>
            !InMemoryCourierRepository.Contains(courierKey)
            || InMemoryCourierRepository.GetById(courierKey).Status != CourierStatus.IsFree);

        _availableCourierKeys.RemoveWhere(courierKey =>
            !_availableCouriers.Contains(courierKey));

        var newOrders = InMemoryOrderRepository.GetNewOrders().ToList();

        foreach (var order in newOrders)
        {
            var courierIndex = _availableCouriers.FindIndex(courierKey =>
                !order.RejectedBy.Contains(courierKey)
                && InMemoryCourierRepository.Contains(courierKey)
                && InMemoryCourierRepository.GetById(courierKey).Status == CourierStatus.IsFree);

            if (courierIndex < 0)
            {
                continue;
            }

            var courierKey = _availableCouriers[courierIndex];
            _availableCouriers.RemoveAt(courierIndex);
            _availableCourierKeys.Remove(courierKey);

            var courierEndPoint = InMemoryCourierRepository.GetEndPointByCourierId(courierKey);

            var bytes = ProtocolServer.EncodeOrder(order, courierKey);

            var key = new PendingMessageKey(order.Id, courierKey, MessageType.ORDER);
            var value = new PendingMessage
            {
                Bytes = bytes,
                Attempts = 0,
                LastSendTime = DateTime.Now
            };
            _pendingMessages.Add(key, value);

            await _socketServer.SendToAsync(bytes, courierEndPoint, token);

            order.CourierId = courierKey;
            order.Status = OrderStatus.Proposed;
            order.Attempts++;
            order.LastSentAt = Environment.TickCount64;

            var courierInfo = InMemoryCourierRepository.GetById(courierKey);
            courierInfo.Status = CourierStatus.IsBusy;
            courierInfo.CurrentOrderId = order.Id;

            Console.WriteLine($"[Main] Заказ {order.Id} отправлен курьеру {courierKey}.");
        }
    }
}
