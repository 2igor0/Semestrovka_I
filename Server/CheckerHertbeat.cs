namespace Server;

public static class CheckerHertbeat
{
    public static List<Guid> Check()
    {
        var reassignedOrders = new List<Guid>();

        foreach (var courier in InMemoryCourierRepository.GetAll().Values.ToList())
        {
            if (courier.Status == CourierStatus.NotAvailable)
            {
                continue;
            }

            var silence = DateTime.Now - courier.LastSeen;

            if (courier.Status == CourierStatus.IsFree)
            {
                if (silence.TotalSeconds > ServerConstants.FreeSilenceSeconds)
                {
                    courier.Status = CourierStatus.NotAvailable;
                }

                continue;
            }

            if (silence.TotalSeconds <= ServerConstants.BusyReassignSeconds)
            {
                continue;
            }

            var orderId = courier.CurrentOrderId;
            if (orderId != Guid.Empty)
            {
                var order = InMemoryOrderRepository.GetById(orderId);

                if (order is not null && order.Status != OrderStatus.Completed)
                {
                    order.Status = OrderStatus.New;
                    order.CourierId = Guid.Empty;
                    order.RejectedBy.Add(courier.CourierId);
                    reassignedOrders.Add(orderId);
                }
            }

            courier.CurrentOrderId = Guid.Empty;
            courier.Status = CourierStatus.NotAvailable;
        }

        return reassignedOrders;
    }
}
