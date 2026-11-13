using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Courier.Gui;

public partial class MainWindow : Window
{
    private readonly CourierGuiContext _context;
    private readonly DispatcherTimer _timer;
    private bool _started;

    public MainWindow()
    {
        InitializeComponent();

        _context = CourierGuiContext.Current
            ?? throw new InvalidOperationException("Courier GUI context is not initialized.");

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _timer.Tick += (_, _) => RefreshView();
        _timer.Start();

        RefreshView();
    }

    private void OnStartClick(object? sender, RoutedEventArgs e)
    {
        if (_context.Commands.TryWrite(CourierEvent.UserCommand("start")))
        {
            _started = true;
            StartButton.IsEnabled = false;
            StatusText.Text = "Выход на линию...";
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        _context.Commands.TryWrite(CourierEvent.UserCommand("cancel"));
    }

    private void OnDoneClick(object? sender, RoutedEventArgs e)
    {
        _context.Commands.TryWrite(CourierEvent.UserCommand("done"));
    }

    private void RefreshView()
    {
        var snapshot = _context.State.Snapshot;
        var hasOrder = snapshot.CurrentOrderId != Guid.Empty;

        OrderIdText.Text = hasOrder
            ? $"Заказ #{snapshot.CurrentOrderId.ToString("N")[..8].ToUpperInvariant()}"
            : _started ? "Ожидание нового заказа" : "Заказа пока нет";

        AddressText.Text = hasOrder ? snapshot.Address : "Ожидание заказа";
        DescriptionText.Text = hasOrder
            ? snapshot.Description
            : "После выхода на линию заказ появится здесь автоматически.";

        MapAddressText.Text = hasOrder
            ? snapshot.Address
            : _started ? "Ожидание заказа" : "Схема появится после заказа";

        CancelButton.IsEnabled = snapshot.CanCancel;
        DoneButton.IsEnabled = snapshot.DoneAllowed;

        StatusText.Text = snapshot.DoneAllowed
            ? "Можно подтвердить доставку"
            : hasOrder
                ? "Заказ в работе"
                : _started ? "На линии, ожидание заказа" : "Ожидание запуска";
    }
}
