using System.Runtime.InteropServices;

namespace Deltha.Helpers;

public sealed class PowerOptimizationHelper : IDisposable
{
    private readonly System.Windows.Threading.DispatcherTimer _timer = new()
    {
        Interval = TimeSpan.FromSeconds(2)
    };

    private IntPtr _ownWindow;
    private bool _isDisposed;
    private bool _isStarted;

    public bool ShouldPauseAnimations { get; private set; }

    public event EventHandler<bool>? AnimationPauseChanged;

    public void Start(IntPtr ownWindow)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;
        _ownWindow = ownWindow;
        _timer.Tick += Timer_Tick;
        UpdateState();
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e) => UpdateState();

    private void UpdateState()
    {
        var shouldPause = IsBatterySaverEnabled() || IsForegroundApplicationFullscreen(_ownWindow);
        if (shouldPause == ShouldPauseAnimations)
        {
            return;
        }

        ShouldPauseAnimations = shouldPause;
        AnimationPauseChanged?.Invoke(this, shouldPause);
    }

    private static bool IsBatterySaverEnabled()
    {
        if (!GetSystemPowerStatus(out var status))
        {
            return false;
        }

        return status.SystemStatusFlag == 1;
    }

    private static bool IsForegroundApplicationFullscreen(IntPtr ownWindow)
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero || foregroundWindow == ownWindow
            || IsIconic(foregroundWindow) || !GetWindowRect(foregroundWindow, out var windowRect))
        {
            return false;
        }

        var monitor = MonitorFromWindow(foregroundWindow, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        return Math.Abs(windowRect.Left - monitorInfo.Monitor.Left) <= 2
            && Math.Abs(windowRect.Top - monitorInfo.Monitor.Top) <= 2
            && Math.Abs(windowRect.Right - monitorInfo.Monitor.Right) <= 2
            && Math.Abs(windowRect.Bottom - monitorInfo.Monitor.Bottom) <= 2;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
    }

    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out Rect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
