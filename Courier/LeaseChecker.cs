namespace Courier;

public static class LeaseChecker
{
    public static bool TryAbandon(CourierState state, out Guid orderId)
    {
        var threshold = TimeSpan.FromSeconds(CourierConstants.ServerSilenceSeconds);
        return state.TryAbandonIfServerSilent(threshold, out orderId);
    }
}
