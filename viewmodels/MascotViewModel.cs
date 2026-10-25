using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Forms;
using System.Diagnostics;

namespace Deltha.ViewModels;

public enum MascotState
{
    Sleep,
    Awake,
    Focus,
    SeeNoEvil,
    HearNoEvil,
    SpeakNoEvil
}

public partial class MascotViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _idleTimer;
    private readonly DispatcherTimer _statusTimer;
    private bool _isRunning;

    [ObservableProperty]
    private string _currentTime = DateTime.Now.ToString("HH:mm:ss");

    [ObservableProperty]
    private int _batteryPercent;

    [ObservableProperty]
    private bool _isCharging;

    [ObservableProperty]
    private bool _isIdle = true;

    [ObservableProperty]
    private bool _isDoNotDisturb;

    [ObservableProperty]
    private bool _isAudioMuted;

    [ObservableProperty]
    private bool _isMicrophoneMuted;

    [ObservableProperty]
    private bool _isDropTargetActive;

    [ObservableProperty]
    private bool _isFocusActive;

    [ObservableProperty]
    private string _systemNotification = string.Empty;

    [ObservableProperty]
    private bool _isSystemNotificationVisible;

    [ObservableProperty]
    private string _updateNotification = string.Empty;

    [ObservableProperty]
    private string _updateDownloadUrl = string.Empty;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private bool _isAnimationPaused;

    public MascotViewModel()
    {
        _idleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _idleTimer.Tick += IdleTimer_Tick;

        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _statusTimer.Tick += StatusTimer_Tick;

        RefreshSystemStatus();
    }

    public MascotState CurrentState =>
        IsDropTargetActive ? MascotState.Awake
        : IsFocusActive ? MascotState.Focus
        : IsDoNotDisturb ? MascotState.SeeNoEvil
        : IsMicrophoneMuted ? MascotState.SpeakNoEvil
        : IsAudioMuted ? MascotState.HearNoEvil
        : IsIdle ? MascotState.Sleep
        : MascotState.Awake;

    public string MascotEmoji => CurrentState switch
    {
        MascotState.Sleep => "💤 🙈",
        MascotState.Awake => "🐵",
        MascotState.Focus => "🤓",
        MascotState.SeeNoEvil => "🙈",
        MascotState.HearNoEvil => "🙉",
        MascotState.SpeakNoEvil => "🙊",
        _ => "🐵"
    };

    public string StateLabel => CurrentState switch
    {
        MascotState.Sleep => "Sleep",
        MascotState.Awake => "Awake",
        MascotState.Focus => "Focus",
        MascotState.SeeNoEvil => "Do Not Disturb",
        MascotState.HearNoEvil => "Audio Muted",
        MascotState.SpeakNoEvil => "Microphone Muted",
        _ => "Awake"
    };

    public string BatteryStatus => IsCharging ? "⚡ Charging" : "Battery";

    public void Start()
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        _statusTimer.Start();
        RecordMouseActivity();
    }

    public void Stop()
    {
        _isRunning = false;
        _idleTimer.Stop();
        _statusTimer.Stop();
    }

    public void RecordMouseActivity()
    {
        if (!_isRunning)
        {
            return;
        }

        IsIdle = false;
        _idleTimer.Stop();
        _idleTimer.Start();
    }

    [RelayCommand]
    private void ToggleDoNotDisturb() => IsDoNotDisturb = !IsDoNotDisturb;

    [RelayCommand]
    private void ToggleAudioMuted() => IsAudioMuted = !IsAudioMuted;

    [RelayCommand]
    private void ToggleMicrophoneMuted() => IsMicrophoneMuted = !IsMicrophoneMuted;

    [RelayCommand]
    private void OpenUpdate()
    {
        if (Uri.TryCreate(UpdateDownloadUrl, UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "http")
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
    }

    partial void OnIsIdleChanged(bool value) => NotifyMascotStateChanged();

    partial void OnIsDoNotDisturbChanged(bool value) => NotifyMascotStateChanged();

    partial void OnIsAudioMutedChanged(bool value) => NotifyMascotStateChanged();

    partial void OnIsMicrophoneMutedChanged(bool value) => NotifyMascotStateChanged();

    partial void OnIsDropTargetActiveChanged(bool value) => NotifyMascotStateChanged();

    partial void OnIsFocusActiveChanged(bool value) => NotifyMascotStateChanged();

    partial void OnIsChargingChanged(bool value) => OnPropertyChanged(nameof(BatteryStatus));

    private void NotifyMascotStateChanged()
    {
        OnPropertyChanged(nameof(CurrentState));
        OnPropertyChanged(nameof(MascotEmoji));
        OnPropertyChanged(nameof(StateLabel));
    }

    private void IdleTimer_Tick(object? sender, EventArgs e)
    {
        _idleTimer.Stop();
        IsIdle = true;
    }

    private void StatusTimer_Tick(object? sender, EventArgs e)
    {
        RefreshSystemStatus();
    }

    private void RefreshSystemStatus()
    {
        CurrentTime = DateTime.Now.ToString("HH:mm:ss");

        var powerStatus = SystemInformation.PowerStatus;
        var batteryLifePercent = powerStatus.BatteryLifePercent;
        BatteryPercent = float.IsFinite(batteryLifePercent) && batteryLifePercent is >= 0 and <= 1
            ? (int)Math.Round(batteryLifePercent * 100)
            : 0;
        IsCharging = powerStatus.PowerLineStatus == PowerLineStatus.Online;
    }

    public void Dispose()
    {
        Stop();
        _idleTimer.Tick -= IdleTimer_Tick;
        _statusTimer.Tick -= StatusTimer_Tick;
    }
}
