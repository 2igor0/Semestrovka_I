using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Courier;
using Courier.Gui;
using Protocol;

// dotnet run --project Courier -- --profile courier1

var socket = new Socket(
    AddressFamily.InterNetwork, 
    SocketType.Dgram, 
    ProtocolType.Udp); 

socket.Bind(new IPEndPoint(IPAddress.Any, 0));

const string defaultProfile = "courier1";
var profile = defaultProfile;

var profileArgumentIndex = Array.IndexOf(args, "--profile");
if (profileArgumentIndex >= 0)
{
    if (profileArgumentIndex + 1 >= args.Length)
    {
        Console.WriteLine("После --profile нужно указать имя профиля.");
        return;
    }

    profile = args[profileArgumentIndex + 1].Trim();
}

if (string.IsNullOrWhiteSpace(profile)
    || profile.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
    || profile.Contains(Path.DirectorySeparatorChar)
    || profile.Contains(Path.AltDirectorySeparatorChar))
{
    Console.WriteLine($"Некорректное имя профиля: {profile}");
    return;
}

var profilesDirectory = FindProfilesDirectory();
if (profilesDirectory is null)
{
    Console.WriteLine("Не удалось найти папку profiles.");
    return;
}

var path = Path.Combine(profilesDirectory, $"{profile}.key");
if (!File.Exists(path))
{
    Console.WriteLine($"Файл профиля не найден: {path}");
    return;
}

var text = File.ReadAllText(path).Trim();

if (!Guid.TryParse(text, out var courierKey))
{
    Console.WriteLine($"В файле {path} нет корректного GUID.");
    return;
}

Console.WriteLine($"Профиль: {profile}");
Console.WriteLine(
    $"GUI: DISPLAY={Environment.GetEnvironmentVariable("DISPLAY") ?? "<нет>"}, " +
    $"WAYLAND_DISPLAY={Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") ?? "<нет>"}");


Console.WriteLine("Для выхода на линию введи start: ");
var command = Console.ReadLine();
while (command != "start")
{
    Console.WriteLine("Для выхода на линию все-таки введите start: ");
    command = Console.ReadLine();
}

var events = Channel.CreateUnbounded<CourierEvent>();
var state = new CourierState(courierKey);
CourierGuiContext.Current = new CourierGuiContext(state, events.Writer);

using var cts = new CancellationTokenSource();

_ = ProtocolHandler.AcceptUdp(socket, events.Writer, cts.Token);
_ = Task.Run(() => ConsoleReader.RunAsync(events.Writer, cts.Token), cts.Token);
_ = Ticker.TimerStart(events.Writer, cts.Token);
_ = CourierRuntime.RunAsync(socket, state, events.Reader, events.Writer, cts.Token);

try
{
    Console.WriteLine("Запускаю GUI...");
    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    Console.WriteLine("GUI закрыт.");
}
catch (Exception exception)
{
    Console.WriteLine($"GUI не запустился: {exception}");
}
finally
{
    cts.Cancel();
}

static AppBuilder BuildAvaloniaApp()
{
    return AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
}

static string? FindProfilesDirectory()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        var candidate = Path.Combine(directory.FullName, "profiles");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        directory = directory.Parent;
    }

    var currentDirectoryCandidate = Path.Combine(Directory.GetCurrentDirectory(), "profiles");
    return Directory.Exists(currentDirectoryCandidate) ? currentDirectoryCandidate : null;
}
