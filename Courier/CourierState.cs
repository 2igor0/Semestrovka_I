using Protocol.@enum;

namespace Courier;

public sealed class CourierState
{
    private record PendingKey(MessageType Type, Guid OrderId);

    private sealed class PendingEntry
    {
        public byte[] Bytes { get; set; } = [];
        public int Attempts { get; set; }
        public DateTime LastSendTime { get; set; }
    }

    private readonly Dictionary<PendingKey, PendingEntry> _outbox = new();

    private int _attemptId;
    private bool _deliveryActive;
    private bool _doneAllowed;

    public CourierState(Guid courierId)
    {
        CourierId = courierId;
        Snapshot = new CourierSnapshot(Guid.Empty, string.Empty, 
            string.Empty, false, false);
    }

    public Guid CourierId { get; }
    public Guid CurrentOrderId { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTime LastServerSeen { get; private set; } = DateTime.Now;
    public CourierSnapshot Snapshot { get; private set; }

    public void RefreshSnapshot()
    {
        Snapshot = new CourierSnapshot(
            CurrentOrderId,
            Address,
            Description,
            _deliveryActive,
            _doneAllowed);
    }

    public void MarkServerSeen()
    {
        LastServerSeen = DateTime.Now;
    }

    public bool TryAcceptOrder(Guid orderId, string address, string description)
    {
        if (CurrentOrderId == Guid.Empty)
        {
            CurrentOrderId = orderId;
            Address = address;
            Description = description;
            _deliveryActive = false;
            _doneAllowed = false;
            _attemptId++;
            return true;
        }

        if (CurrentOrderId == orderId)
        {
            Address = address;
            Description = description;
            return true;
        }

        return false;
    }

    public bool TryBeginDelivery(Guid orderId, out int attemptId, out bool startNewTimer)
    {
        attemptId = _attemptId;
        startNewTimer = false;

        if (CurrentOrderId == Guid.Empty || CurrentOrderId != orderId)
        {
            return false;
        }

        if (_deliveryActive)
        {
            return true;
        }

        _deliveryActive = true;
        _doneAllowed = false;
        _attemptId++;
        attemptId = _attemptId;
        startNewTimer = true;
        return true;
    }

    public bool MarkDeliveryReady(Guid orderId, int attemptId)
    {
        if (CurrentOrderId != orderId
            || _attemptId != attemptId
            || !_deliveryActive)
        {
            return false;
        }

        _doneAllowed = true;
        return true;
    }

    public bool TryCompleteDelivery(out Guid orderId)
    {
        orderId = CurrentOrderId;

        if (CurrentOrderId == Guid.Empty || !_deliveryActive || !_doneAllowed)
        {
            return false;
        }

        ClearCurrentOrder();
        return true;
    }

    public bool TryRevoke(Guid orderId)
    {
        DropPendingForOrder(orderId);

        if (CurrentOrderId == Guid.Empty || CurrentOrderId != orderId)
        {
            return false;
        }

        ClearCurrentOrder();
        return true;
    }

    public bool TryCancelCurrent(out Guid orderId)
    {
        orderId = CurrentOrderId;

        if (CurrentOrderId == Guid.Empty)
        {
            return false;
        }

        DropPendingForOrder(CurrentOrderId);
        ClearCurrentOrder();
        return true;
    }

    public bool TryAbandonIfServerSilent(TimeSpan silenceThreshold, out Guid orderId)
    {
        orderId = CurrentOrderId;

        if (CurrentOrderId == Guid.Empty
            || !_deliveryActive
            || DateTime.Now - LastServerSeen <= silenceThreshold)
        {
            return false;
        }

        DropPendingForOrder(CurrentOrderId);
        ClearCurrentOrder();
        return true;
    }

    public bool IsOrderMismatch(Guid orderId)
    {
        return CurrentOrderId != Guid.Empty && CurrentOrderId != orderId;
    }

    public void TrackPending(MessageType type, Guid orderId, byte[] bytes)
    {
        _outbox[new PendingKey(type, orderId)] = new PendingEntry
        {
            Bytes = bytes,
            Attempts = 0,
            LastSendTime = DateTime.Now
        };
    }

    public bool TryRemovePending(MessageType type, Guid orderId)
    {
        return _outbox.Remove(new PendingKey(type, orderId));
    }

    public List<byte[]> CollectDue()
    {
        var due = new List<byte[]>();

        foreach (var (key, entry) in _outbox.ToList())
        {
            if ((DateTime.Now - entry.LastSendTime).TotalSeconds < CourierConstants.ResendIntervalSeconds)
            {
                continue;
            }

            if (entry.Attempts >= CourierConstants.MaxResendAttempts)
            {
                _outbox.Remove(key);
                continue;
            }

            entry.Attempts++;
            entry.LastSendTime = DateTime.Now;
            due.Add(entry.Bytes);
        }

        return due;
    }

    private void DropPendingForOrder(Guid orderId)
    {
        foreach (var key in _outbox.Keys.Where(key => key.OrderId == orderId).ToList())
        {
            _outbox.Remove(key);
        }
    }

    private void ClearCurrentOrder()
    {
        CurrentOrderId = Guid.Empty;
        Address = string.Empty;
        Description = string.Empty;
        _deliveryActive = false;
        _doneAllowed = false;
        _attemptId++;
    }
}
