using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Deltha.Helpers;

public static class ClickThroughHelper
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private static readonly ConditionalWeakTable<Window, PointerMonitor> Monitors = new();

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    public static void EnableClickThrough(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
        StartPointerMonitor(window);
    }

    public static void DisableClickThrough(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        if (Monitors.TryGetValue(window, out var monitor))
        {
            monitor.Stop();
        }

        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
    }

    private static void StartPointerMonitor(Window window)
    {
        var island = window.FindName("IslandBorder") as FrameworkElement
            ?? throw new InvalidOperationException("The window must contain an IslandBorder element.");

        var monitor = Monitors.GetValue(window, key => new PointerMonitor(key, island));
        monitor.Start();
    }

    private sealed class PointerMonitor
    {
        private readonly Window _window;
        private readonly FrameworkElement _island;
        private readonly DispatcherTimer _timer = new()
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };

        public PointerMonitor(Window window, FrameworkElement island)
        {
            _window = window;
            _island = island;
            _timer.Tick += MonitorPointerPosition;
        }

        public void Start() => _timer.Start();

        public void Stop() => _timer.Stop();

        private void MonitorPointerPosition(object? sender, EventArgs e)
        {
            var cursor = System.Windows.Forms.Cursor.Position;
            var point = _island.PointFromScreen(new System.Windows.Point(cursor.X, cursor.Y));
            if (point.X >= 0 && point.Y >= 0
                && point.X < _island.ActualWidth && point.Y < _island.ActualHeight)
            {
                DisableClickThrough(_window);
            }
        }
    }
}