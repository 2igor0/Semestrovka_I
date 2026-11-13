using System.Net.Sockets;
using System.Threading.Channels;
using Protocol;
using Protocol.Entities;
using Protocol.@enum;

namespace Courier;

public static class CourierRuntime
{
    public static async Task RunAsync(
        Socket socketCourier,
        CourierState state,
        ChannelReader<CourierEvent> reader,
        ChannelWriter<CourierEvent> writer,
        CancellationToken token)
    {
        await foreach (var courierEvent in reader.ReadAllAsync(token))
        {
            switch (courierEvent.Kind)
            {
                case CourierEventKind.IncomingMessage:
                    await HandleIncomingMessageAsync(
                        socketCourier,
                        state,
                        courierEvent.Message!,
                        writer,
                        token);
                    break;

                case CourierEventKind.UserCommand:
                    await HandleCommandAsync(
                        socketCourier,
                        state,
                        courierEvent.Command!,
                        token);
                    break;

                case CourierEventKind.Tick:
                    await HandleTickAsync(socketCourier, state, token);
                    break;

                case CourierEventKind.DeliveryReady:
                    if (state.MarkDeliveryReady(courierEvent.OrderId, courierEvent.AttemptId))
                    {
                        Console.WriteLine("Введите done");
                    }

                    break;
            }

            state.RefreshSnapshot();
        }
    }

    private static async Task HandleIncomingMessageAsync(
        Socket socketCourier,
        CourierState state,
        ProtocolMessage message,
        ChannelWriter<CourierEvent> writer,
        CancellationToken token)
    {
        state.MarkServerSeen();

        switch (message.MessageType)
        {
            case MessageType.ORDER:
                var order = ProtocolCourier.ParseOrder(message.Payload);

                if (order is null)
                {
                    Console.WriteLine($"Некорректная датаграмма заказа: {message.OrderId}.");
                    break;
                }

                if (!state.TryAcceptOrder(message.OrderId, order.Address, order.Description))
                {
                    Console.WriteLine("Занят другим заказом, новый заказ проигнорирован.");
                    break;
                }

                var readyBytes = ProtocolCourier.EncodeReady(message.OrderId, message.CourierId);
                state.TrackPending(MessageType.READY, message.OrderId, readyBytes);
                await ProtocolHandler.SendAsync(socketCourier, readyBytes, token);

                Console.WriteLine($"Получен заказ {message.OrderId}.");
                Console.WriteLine($"Адрес: {order.Address}");
                Console.WriteLine($"Описание: {order.Description}");
                break;

            case MessageType.GO:
                if (!state.TryBeginDelivery(
                        message.OrderId,
                        out var attemptId,
                        out var startNewTimer))
                {
                    break;
                }

                var startedBytes = ProtocolCourier.EncodeStarted(message.CourierId, message.OrderId);
                state.TryRemovePending(MessageType.READY, message.OrderId);
                state.TrackPending(MessageType.STARTED, message.OrderId, startedBytes);
                await ProtocolHandler.SendAsync(socketCourier, startedBytes, token);

                if (startNewTimer)
                {
                    _ = DeliveryTimer.RunAsync(writer, message.OrderId, attemptId, token);
                }

                break;

            case MessageType.HEARTBEAT:
                if (state.IsOrderMismatch(message.OrderId))
                {
                    Console.WriteLine(
                        $"Heartbeat: сервер считает заказ {message.OrderId}, локально {state.CurrentOrderId}. Оставляю текущий.");
                }

                break;

            case MessageType.REVOKED:
                if (state.TryRevoke(message.OrderId))
                {
                    Console.WriteLine($"Заказ {message.OrderId} отозван.");
                }

                break;

            case MessageType.ACK:
                if (GeneralParser.TryParseAckedType(message.Payload, out var ackedType))
                {
                    state.TryRemovePending(ackedType, message.OrderId);
                }

                break;

            default:
                Console.WriteLine($"Неизвестный тип сообщения: {message.MessageType}");
                break;
        }
    }

    private static async Task HandleCommandAsync(
        Socket socketCourier,
        CourierState state,
        string command,
        CancellationToken token)
    {
        if (command.Equals("start", StringComparison.OrdinalIgnoreCase))
        {
            var startBytes = ProtocolCourier.EncodeStart(state.CourierId);
            state.TrackPending(MessageType.START, Guid.Empty, startBytes);
            await ProtocolHandler.SendAsync(socketCourier, startBytes, token);
            Console.WriteLine("Курьер вышел на линию.");
            return;
        }

        if (command.Equals("done", StringComparison.OrdinalIgnoreCase))
        {
            if (!state.TryCompleteDelivery(out var orderId))
            {
                Console.WriteLine("Пока нельзя подтвердить доставку.");
                return;
            }

            var deliveredBytes = ProtocolCourier.EncodeDelivered(state.CourierId, orderId);
            state.TrackPending(MessageType.DELIVERED, orderId, deliveredBytes);
            await ProtocolHandler.SendAsync(socketCourier, deliveredBytes, token);
            Console.WriteLine($"Заказ {orderId} доставлен.");
            return;
        }

        if (command.Equals("cancel", StringComparison.OrdinalIgnoreCase))
        {
            if (!state.TryCancelCurrent(out var orderId))
            {
                Console.WriteLine("Нет активного заказа.");
                return;
            }

            var cancelBytes = ProtocolCourier.EncodeCancel(state.CourierId, orderId);
            state.TrackPending(MessageType.CANCEL, orderId, cancelBytes);
            await ProtocolHandler.SendAsync(socketCourier, cancelBytes, token);
            Console.WriteLine($"Заказ {orderId} отменён, серверу отправлен CANCEL.");
            return;
        }

        Console.WriteLine("Неправильный ввод");
    }

    private static async Task HandleTickAsync(
        Socket socketCourier,
        CourierState state,
        CancellationToken token)
    {
        if (LeaseChecker.TryAbandon(state, out var abandonedOrderId))
        {
            Console.WriteLine(
                $"Сервер молчит дольше {CourierConstants.ServerSilenceSeconds} секунд. Заказ {abandonedOrderId} отменён локально.");
        }

        foreach (var bytes in state.CollectDue())
        {
            await ProtocolHandler.SendAsync(socketCourier, bytes, token);
        }

        await HeartbeatSender.SendAsync(
            socketCourier,
            state.CourierId,
            state.CurrentOrderId,
            token);
    }
}
