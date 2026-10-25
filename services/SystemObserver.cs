using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Windows.Devices.Radios;

namespace Deltha.Services;

public sealed class SystemObserver : IDisposable
{
    private readonly System.Threading.Timer _pollTimer;
    private readonly DispatcherTimer _radioPollTimer = new() { Interval = TimeSpan.FromSeconds(8) };
    private uint? _lastVolume;
    private uint? _lastBrightness;
    private PowerLineStatus? _lastPowerLineStatus;
    private NetworkAvailabilityEventArgs? _lastNetworkState;
    private string? _lastRadioState;
    private bool? _lastWifiState;
    private bool _isDisposed;
    private bool _isStarted;
    private int _isPolling;

    public SystemObserver()
    {
        _pollTimer = new System.Threading.Timer(
            PollTimer_Tick,
            null,
            Timeout.Infinite,
            Timeout.Infinite);
    }

    public event EventHandler<string>? SystemEventDetected;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;
        NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
        _radioPollTimer.Tick += RadioPollTimer_Tick;
        _pollTimer.Change(0, 2000);
        _radioPollTimer.Start();
        _ = PollBluetoothStateAsync();
    }

    private void PollTimer_Tick(object? state)
    {
        if (Interlocked.Exchange(ref _isPolling, 1) != 0)
        {
            return;
        }

        try
        {
            var volume = GetVolume();
            if (volume is not null && _lastVolume is not null && volume != _lastVolume)
            {
                SystemEventDetected?.Invoke(this, $"Volume changed · {volume.Value * 100 / uint.MaxValue}%");
            }
            _lastVolume = volume;

            var brightness = GetBrightness();
            if (brightness is not null && _lastBrightness is not null && brightness != _lastBrightness)
            {
                SystemEventDetected?.Invoke(this, $"Brightness changed · {brightness}%");
            }
            _lastBrightness = brightness;

            var powerLine = SystemInformation.PowerStatus.PowerLineStatus;
            if (_lastPowerLineStatus is not null
                && powerLine != _lastPowerLineStatus
                && powerLine is PowerLineStatus.Online or PowerLineStatus.Offline)
            {
                SystemEventDetected?.Invoke(
                    this,
                    powerLine == PowerLineStatus.Online ? "Charger connected" : "Charger disconnected");
            }
            _lastPowerLineStatus = powerLine;

            var wifiConnected = NetworkInterface.GetAllNetworkInterfaces()
                .Where(network => network.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .Any(network => network.OperationalStatus == OperationalStatus.Up);
            if (_lastWifiState is not null && wifiConnected != _lastWifiState)
            {
                SystemEventDetected?.Invoke(
                    this,
                    wifiConnected ? "Wi-Fi connected" : "Wi-Fi disconnected");
            }
            _lastWifiState = wifiConnected;
        }
        finally
        {
            Volatile.Write(ref _isPolling, 0);
        }
    }

    private async void RadioPollTimer_Tick(object? sender, EventArgs e)
    {
        await PollBluetoothStateAsync();
    }

    private async Task PollBluetoothStateAsync()
    {
        try
        {
            var radios = await Radio.GetRadiosAsync();
            var bluetoothState = radios
                .Where(radio => radio.Kind == RadioKind.Bluetooth)
                .Select(radio => radio.State.ToString())
                .FirstOrDefault() ?? "Unavailable";

            if (_lastRadioState is not null && bluetoothState != _lastRadioState)
            {
                SystemEventDetected?.Invoke(
                    this,
                    bluetoothState == RadioState.On.ToString()
                        ? "Bluetooth enabled"
                        : "Bluetooth disabled");
            }

            _lastRadioState = bluetoothState;
        }
        catch (Exception exception)
        {
            if (_lastRadioState != "Error")
            {
                SystemEventDetected?.Invoke(this, $"Bluetooth status unavailable: {exception.Message}");
            }
            _lastRadioState = "Error";
        }
    }

    private void NetworkChange_NetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (_lastNetworkState is not null && e.IsAvailable != _lastNetworkState.IsAvailable)
        {
            SystemEventDetected?.Invoke(
                this,
                e.IsAvailable ? "Network connected" : "Network disconnected");
        }

        _lastNetworkState = e;
    }

    private void NetworkChange_NetworkAddressChanged(object? sender, EventArgs e)
    {
        ThreadPool.QueueUserWorkItem(PollTimer_Tick);
    }

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.StatusChange)
        {
            SystemEventDetected?.Invoke(this, "Power status changed");
        }
    }

    private static uint? GetVolume()
    {
        return waveOutGetVolume(IntPtr.Zero, out var volume) == 0 ? volume : null;
    }

    private static uint? GetBrightness()
    {
        var monitor = MonitorFromPoint(new Point { X = 1, Y = 1 }, MonitorDefaultToPrimary);
        if (monitor == IntPtr.Zero)
        {
            return null;
        }

        if (!GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out var monitorCount) || monitorCount == 0)
        {
            return null;
        }

        var monitors = new PhysicalMonitor[monitorCount];
        if (!GetPhysicalMonitorsFromHMONITOR(monitor, monitorCount, monitors))
        {
            return null;
        }

        try
        {
            foreach (var physicalMonitor in monitors)
            {
                if (GetMonitorBrightness(
                    physicalMonitor.Handle,
                    out _,
                    out var currentBrightness,
                    out _))
                {
                    return currentBrightness;
                }
            }

            return null;
        }
        finally
        {
            DestroyPhysicalMonitors((uint)monitors.Length, monitors);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _pollTimer.Dispose();
        _radioPollTimer.Stop();
        _radioPollTimer.Tick -= RadioPollTimer_Tick;
        NetworkChange.NetworkAvailabilityChanged -= NetworkChange_NetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged -= NetworkChange_NetworkAddressChanged;
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
    }

    private const uint MonitorDefaultToPrimary = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PhysicalMonitor
    {
        public IntPtr Handle;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
    }

    [DllImport("winmm.dll")]
    private static extern int waveOutGetVolume(IntPtr device, out uint volume);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point point, uint flags);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
        IntPtr monitor,
        out uint numberOfPhysicalMonitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(
        IntPtr monitor,
        uint physicalMonitorArraySize,
        [Out] PhysicalMonitor[] physicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorBrightness(
        IntPtr monitor,
        out uint minimumBrightness,
        out uint currentBrightness,
        out uint maximumBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyPhysicalMonitors(
        uint physicalMonitorArraySize,
        [In] PhysicalMonitor[] physicalMonitorArray);
}
