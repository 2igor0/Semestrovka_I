using System.Net;
using System.Net.Sockets;
using Protocol;
using Protocol.@enum;

namespace Courier;

public static class ProtocolHandler
{
    private static readonly IPEndPoint Server = new(IPAddress.Parse("127.0.0.1"), 5050);
    private static readonly SemaphoreSlim SendGate = new(1, 1);

    public static async Task AcceptUdp(Socket socketCourier, CourierSession session, CancellationToken token)
    {
        var remoteIp = new IPEndPoint(IPAddress.Any, 0);
        var bytes = new byte[512];

        while (!token.IsCancellationRequested)
        {
            var result = await socketCourier.ReceiveFromAsync(bytes, remoteIp, token);
            var datagram = bytes.AsSpan(0, result.ReceivedBytes);

            var parseAnswer = GeneralParser.Pars(datagram);
            if (parseAnswer is null)
            {
                continue;
            }

            if (parseAnswer.MessageType == MessageType.ORDER)
            {
                var order = ProtocolCourier.ParseOrder(parseAnswer.Payload);

                if (order is null)
                {
                    Console.WriteLine($"Некорректная датаграмма: {result.ReceivedBytes} байт.");
                    continue;
                }

                var orderAckBytes = GeneralEncoder.EncodeAck(parseAnswer.CourierId, parseAnswer.OrderId, MessageType.ORDER);
                await SendAsync(socketCourier, orderAckBytes, token);

                if (!session.TryAcceptOrder(parseAnswer.OrderId, parseAnswer.CourierId))
                {
                    Console.WriteLine("Занят другим заказом, новый заказ проигнорирован.");
                    continue;
                }

                var readyBytes = ProtocolCourier.EncodeReady(parseAnswer.OrderId, parseAnswer.CourierId);
                session.TrackPending(MessageType.READY, parseAnswer.OrderId, readyBytes);
                await SendAsync(socketCourier, readyBytes, token);

                Console.WriteLine($"Получен заказ {parseAnswer.OrderId}.");
                Console.WriteLine($"Адрес: {order.Address}");
                Console.WriteLine($"Описание: {order.Description}");
            }
            else if (parseAnswer.MessageType == MessageType.GO)
            {
                var goAckBytes = GeneralEncoder.EncodeAck(parseAnswer.CourierId, parseAnswer.OrderId, MessageType.GO);
                await SendAsync(socketCourier, goAckBytes, token);

                if (!session.TryBeginDelivery(
                        parseAnswer.OrderId,
                        parseAnswer.CourierId,
                        out var attemptId,
                        out var cancellationToken,
                        out var startNewTimer))
                {
                    continue;
                }

                var startedBytes = ProtocolCourier.EncodeStarted(parseAnswer.CourierId, parseAnswer.OrderId);
                session.TryRemovePending(MessageType.READY, parseAnswer.OrderId);
                session.TrackPending(MessageType.STARTED, parseAnswer.OrderId, startedBytes);
                await SendAsync(socketCourier, startedBytes, token);

                if (startNewTimer)
                {
                    _ = WaitDeliveryAsync(session, parseAnswer.OrderId, attemptId, cancellationToken);
                }
            }
            else if (parseAnswer.MessageType == MessageType.REVOKED)
            {
                if (session.TryRevoke(parseAnswer.OrderId))
                {
                    Console.WriteLine($"Заказ {parseAnswer.OrderId} отозван.");
                }
            }
            else if (parseAnswer.MessageType == MessageType.ACK)
            {
                if (GeneralParser.TryParseAckedType(parseAnswer.Payload, out var ackedType))
                {
                    session.TryRemovePending(ackedType, parseAnswer.OrderId);
                }
            }
            else
            {
                Console.WriteLine($"Неизвестный тип сообщения: {parseAnswer.MessageType}");
            }
        }
    }

    public static async Task ReadCommandsAsync(Socket socketCourier, CourierSession session, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var answer = Console.ReadLine();
            if (answer is null)
            {
                return;
            }

            if (answer.Trim().Equals("done", StringComparison.OrdinalIgnoreCase))
            {
                if (session.TryCompleteDelivery(out var orderId, out var courierId))
                {
                    var deliveredBytes = ProtocolCourier.EncodeDelivered(courierId, orderId);
                    session.TrackPending(MessageType.DELIVERED, orderId, deliveredBytes);
                    await SendAsync(socketCourier, deliveredBytes, token);
                    Console.WriteLine($"Заказ {orderId} доставлен.");
                }
                else
                {
                    Console.WriteLine("Пока нельзя подтвердить доставку.");
                }
            }
            else if (answer.Trim().Equals("cancel", StringComparison.OrdinalIgnoreCase))
            {
                if (session.TryCancelCurrent())
                {
                    Console.WriteLine("Доставка отменена локально. Сообщение Cancel серверу пока не отправляется.");
                }
                else
                {
                    Console.WriteLine("Нет активного заказа.");
                }
            }
            else
            {
                Console.WriteLine("Неправильный ввод");
            }
        }
    }

    public static async Task SendAsync(Socket socketCourier, byte[] datagram, CancellationToken token)
    {
        await SendGate.WaitAsync(token);

        try
        {
            await socketCourier.SendToAsync(datagram, Server, token);
        }
        finally
        {
            SendGate.Release();
        }
    }

    private static async Task WaitDeliveryAsync(
        CourierSession session,
        Guid orderId,
        int attemptId,
        CancellationToken token)
    {
        Console.WriteLine("Через 5 секунд вы можете подтвердить, что заказ доставлен!");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (session.MarkDoneAllowed(orderId, attemptId))
        {
            Console.WriteLine("Введите done");
        }
    }
}
