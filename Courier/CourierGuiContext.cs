using System.Threading.Channels;

namespace Courier;

public sealed class CourierGuiContext
{
    public static CourierGuiContext? Current { get; set; }

    public CourierGuiContext(CourierState state, ChannelWriter<CourierEvent> commands)
    {
        State = state;
        Commands = commands;
    }

    public CourierState State { get; }
    public ChannelWriter<CourierEvent> Commands { get; }
}
