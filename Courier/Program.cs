using System.Net;
using System.Net.Sockets;
using System.Text;
using Courier;
using Protocol;
using Protocol.@enum;

// dotnet run --project Courier -- --profile courier1

var socket = new Socket(
    AddressFamily.InterNetwork, 
    SocketType.Dgram, 
    ProtocolType.Udp); 

IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5050);

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


Console.WriteLine("Для выхода на линию введи start: ");
var command = Console.ReadLine();
while (command != "start")
{
    Console.WriteLine("Для выхода на линию все-таки введите start: ");
    command = Console.ReadLine();
}

var buffer = ProtocolCourier.EncodeStart(courierKey);

var session = new CourierSession();
session.TrackPending(MessageType.START, Guid.Empty, buffer);

await socket.SendToAsync(buffer, serverEndpoint);

using var cts = new CancellationTokenSource();
await Task.WhenAll(
    ProtocolHandler.AcceptUdp(socket, session, cts.Token),
    ProtocolHandler.ReadCommandsAsync(socket, session, cts.Token),
    Ticker.TimerStart(socket, session, cts.Token));

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
