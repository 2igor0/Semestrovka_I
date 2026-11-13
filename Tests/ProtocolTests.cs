using Protocol;
using Protocol.Entities;
using Protocol.@enum;
using Server;

namespace Tests;

public class ProtocolTests
{
    [Fact]
    public void Order_roundtrip_preserves_cyrillic_and_separators()
    {
        var order = Order.Create("Пицца, кола и сок", "Ленина 5 | корпус 2");
        var courierId = Guid.NewGuid();

        var datagram = ProtocolServer.EncodeOrder(order, courierId);
        var envelope = GeneralParser.Pars(datagram);

        Assert.NotNull(envelope);
        Assert.Equal(MessageType.ORDER, envelope!.MessageType);
        Assert.Equal(courierId, envelope.CourierId);

        var parsed = ProtocolCourier.ParseOrder(envelope.Payload);

        Assert.NotNull(parsed);
        Assert.Equal(order.Address, parsed!.Address);
        Assert.Equal(order.Description, parsed.Description);
    }

    [Fact]
    public void Parser_returns_null_for_empty_and_truncated_datagrams()
    {
        Assert.Null(GeneralParser.Pars(ReadOnlySpan<byte>.Empty));
        Assert.Null(GeneralParser.Pars(new byte[10]));
    }

    [Fact]
    public void Ack_roundtrip_preserves_acked_type()
    {
        var courierId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var datagram = GeneralEncoder.EncodeAck(courierId, orderId, MessageType.READY);
        var envelope = GeneralParser.Pars(datagram);

        Assert.NotNull(envelope);
        Assert.Equal(MessageType.ACK, envelope!.MessageType);
        Assert.True(GeneralParser.TryParseAckedType(envelope.Payload, out var ackedType));
        Assert.Equal(MessageType.READY, ackedType);
    }

    [Fact]
    public void Ack_parser_rejects_invalid_payload()
    {
        Assert.False(GeneralParser.TryParseAckedType([], out _));
        Assert.False(GeneralParser.TryParseAckedType([255], out _));
        Assert.False(GeneralParser.TryParseAckedType([0, 1], out _));
    }
}
