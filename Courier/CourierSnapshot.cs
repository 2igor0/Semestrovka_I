namespace Courier;

public sealed record CourierSnapshot(
    Guid CurrentOrderId,
    string Address,
    string Description,
    bool DeliveryActive,
    bool DoneAllowed)
{
    public bool CanCancel => CurrentOrderId != Guid.Empty;
}
