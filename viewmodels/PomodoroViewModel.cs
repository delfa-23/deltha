using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Deltha.ViewModels;

public enum PomodoroPhase
{
    Work,
    Break
}

public partial class PomodoroViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan WorkDuration = TimeSpan.FromMinutes(25);
    private static readonly TimeSpan BreakDuration = TimeSpan.FromMinutes(5);
    private readonly DispatcherTimer _timer;
    private DateTime _endTime;

    [ObservableProperty]
    private PomodoroPhase _phase = PomodoroPhase.Work;

    [ObservableProperty]
    private TimeSpan _remaining = WorkDuration;

    [ObservableProperty]
    private bool _isRunning;

    public PomodoroViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;
    }

    public string TimeDisplay => $"{(int)Remaining.TotalMinutes:00}:{Remaining.Seconds:00}";

    public string PhaseLabel => Phase == PomodoroPhase.Work ? "Focus • Work" : "Break";

    public string StartPauseLabel => IsRunning ? "Pause" : "Start";

    public bool IsFocusActive => IsRunning && Phase == PomodoroPhase.Work;

    [RelayCommand]
    private void StartPause()
    {
        if (IsRunning)
        {
            UpdateRemaining();
            _timer.Stop();
            IsRunning = false;
            return;
        }

        _endTime = DateTime.UtcNow + Remaining;
        IsRunning = true;
        _timer.Start();
        NotifyTimerStateChanged();
    }

    [RelayCommand]
    private void Reset()
    {
        _timer.Stop();
        IsRunning = false;
        Remaining = Phase == PomodoroPhase.Work ? WorkDuration : BreakDuration;
        NotifyTimerStateChanged();
    }

    [RelayCommand]
    private void SwitchPhase()
    {
        _timer.Stop();
        IsRunning = false;
        Phase = Phase == PomodoroPhase.Work ? PomodoroPhase.Break : PomodoroPhase.Work;
        Remaining = Phase == PomodoroPhase.Work ? WorkDuration : BreakDuration;
        NotifyTimerStateChanged();
    }

    partial void OnRemainingChanged(TimeSpan value) => OnPropertyChanged(nameof(TimeDisplay));

    partial void OnPhaseChanged(PomodoroPhase value)
    {
        OnPropertyChanged(nameof(PhaseLabel));
        OnPropertyChanged(nameof(IsFocusActive));
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsFocusActive));
        OnPropertyChanged(nameof(StartPauseLabel));
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        UpdateRemaining();
        if (Remaining > TimeSpan.Zero)
        {
            return;
        }

        _timer.Stop();
        System.Media.SystemSounds.Exclamation.Play();
        Phase = Phase == PomodoroPhase.Work ? PomodoroPhase.Break : PomodoroPhase.Work;
        Remaining = Phase == PomodoroPhase.Work ? WorkDuration : BreakDuration;
        _endTime = DateTime.UtcNow + Remaining;
        IsRunning = true;
        _timer.Start();
        NotifyTimerStateChanged();
    }

    private void UpdateRemaining()
    {
        var remaining = _endTime - DateTime.UtcNow;
        Remaining = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private void NotifyTimerStateChanged()
    {
        OnPropertyChanged(nameof(TimeDisplay));
        OnPropertyChanged(nameof(PhaseLabel));
        OnPropertyChanged(nameof(IsFocusActive));
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
    }
}
