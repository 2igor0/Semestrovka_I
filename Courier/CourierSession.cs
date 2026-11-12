namespace Courier;

public sealed class CourierSession
{
    private readonly object _sync = new();

    private Guid _currentOrderId;
    private Guid _courierId;
    private CancellationTokenSource? _deliveryCancellation;
    private int _attemptId;
    private bool _doneAllowed;

    public bool TryAcceptOrder(Guid orderId, Guid courierId)
    {
        lock (_sync)
        {
            if (_currentOrderId == Guid.Empty)
            {
                _currentOrderId = orderId;
                _courierId = courierId;
                _doneAllowed = false;
                _attemptId++;
                return true;
            }

            return _currentOrderId == orderId;
        }
    }

    public bool TryBeginDelivery(
        Guid orderId,
        Guid courierId,
        out int attemptId,
        out CancellationToken cancellationToken,
        out bool startNewTimer)
    {
        lock (_sync)
        {
            attemptId = _attemptId;
            cancellationToken = default;
            startNewTimer = false;

            if (_currentOrderId == Guid.Empty || _currentOrderId != orderId)
            {
                return false;
            }

            _courierId = courierId;

            if (_deliveryCancellation is not null)
            {
                cancellationToken = _deliveryCancellation.Token;
                return true;
            }

            _deliveryCancellation = new CancellationTokenSource();
            _doneAllowed = false;
            _attemptId++;
            attemptId = _attemptId;
            cancellationToken = _deliveryCancellation.Token;
            startNewTimer = true;
            return true;
        }
    }

    public bool MarkDoneAllowed(Guid orderId, int attemptId)
    {
        lock (_sync)
        {
            if (_currentOrderId != orderId
                || _attemptId != attemptId
                || _deliveryCancellation is null)
            {
                return false;
            }

            _doneAllowed = true;
            return true;
        }
    }

    public bool TryCompleteDelivery(out Guid orderId, out Guid courierId)
    {
        lock (_sync)
        {
            orderId = _currentOrderId;
            courierId = _courierId;

            if (_currentOrderId == Guid.Empty
                || !_doneAllowed
                || _deliveryCancellation is null)
            {
                return false;
            }

            CancelCurrentLocked();
            return true;
        }
    }

    public bool TryRevoke(Guid orderId)
    {
        lock (_sync)
        {
            if (_currentOrderId == Guid.Empty || _currentOrderId != orderId)
            {
                return false;
            }

            CancelCurrentLocked();
            return true;
        }
    }

    public bool TryCancelCurrent()
    {
        lock (_sync)
        {
            if (_currentOrderId == Guid.Empty)
            {
                return false;
            }

            CancelCurrentLocked();
            return true;
        }
    }

    private void CancelCurrentLocked()
    {
        _deliveryCancellation?.Cancel();
        _deliveryCancellation?.Dispose();
        _deliveryCancellation = null;
        _currentOrderId = Guid.Empty;
        _courierId = Guid.Empty;
        _doneAllowed = false;
        _attemptId++;
    }
}
