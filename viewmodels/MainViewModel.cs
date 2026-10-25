using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Deltha.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private string _currentTime = DateTime.Now.ToString("HH:mm:ss");
    private int _batteryPercent = 100;
    private bool _isIdle = true;
    private bool _isSleeping;
    private bool _isMuted;
    private bool _isMicrophoneMuted;
    private bool _isDoNotDisturb;

    public string CurrentTime
    {
        get => _currentTime;
        set
        {
            if (_currentTime == value) return;
            _currentTime = value;
            OnPropertyChanged();
        }
    }

    public int BatteryPercent
    {
        get => _batteryPercent;
        set
        {
            if (_batteryPercent == value) return;
            _batteryPercent = value;
            OnPropertyChanged();
        }
    }

    public bool IsIdle
    {
        get => _isIdle;
        set
        {
            if (_isIdle == value) return;
            _isIdle = value;
            OnPropertyChanged();
        }
    }

    public bool IsSleeping
    {
        get => _isSleeping;
        set
        {
            if (_isSleeping == value) return;
            _isSleeping = value;
            OnPropertyChanged();
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (_isMuted == value) return;
            _isMuted = value;
            OnPropertyChanged();
        }
    }

    public bool IsMicrophoneMuted
    {
        get => _isMicrophoneMuted;
        set
        {
            if (_isMicrophoneMuted == value) return;
            _isMicrophoneMuted = value;
            OnPropertyChanged();
        }
    }

    public bool IsDoNotDisturb
    {
        get => _isDoNotDisturb;
        set
        {
            if (_isDoNotDisturb == value) return;
            _isDoNotDisturb = value;
            OnPropertyChanged();
        }
    }

    public bool IsClickThroughEnabled => IsIdle || IsSleeping;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
