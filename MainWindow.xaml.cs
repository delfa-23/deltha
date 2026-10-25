using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using System.IO;
using Deltha.Helpers;
using Deltha.Services;
using Deltha.ViewModels;
using Deltha.Views;

namespace Deltha;

public partial class MainWindow : Window
{
    private const int WM_NCHITTEST = 0x0084;
    private const int HTCLIENT = 1;
    private const int HTTRANSPARENT = -1;
    private const double IslandCornerRadius = 25;

    private readonly MascotViewModel _viewModel;
    private readonly MediaViewModel _mediaViewModel;
    public FileDropViewModel DropViewModel { get; } = new();
    public PomodoroViewModel PomodoroViewModel { get; } = new();
    public LauncherViewModel LauncherViewModel { get; } = new();
    private readonly SystemObserver _systemObserver = new();
    private readonly PowerOptimizationHelper _powerOptimizationHelper = new();
    private readonly DispatcherTimer _notificationTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Storyboard _notificationPulse = new();
    private double _normalWindowWidth = 380;
    private bool _notificationPulseActive;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MascotViewModel();
        _mediaViewModel = new MediaViewModel(new MediaService(), new LyricsService());
        DataContext = _viewModel;
        MediaPanel.DataContext = _mediaViewModel;
        PomodoroViewModel.PropertyChanged += PomodoroViewModel_PropertyChanged;
        _viewModel.IsFocusActive = PomodoroViewModel.IsFocusActive;
        _systemObserver.SystemEventDetected += SystemObserver_SystemEventDetected;
        _powerOptimizationHelper.AnimationPauseChanged += PowerOptimizationHelper_AnimationPauseChanged;
        _notificationTimer.Tick += NotificationTimer_Tick;
        var pulse = new DoubleAnimation(0.88, 1, TimeSpan.FromMilliseconds(600))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        Storyboard.SetTargetProperty(pulse, new PropertyPath(UIElement.OpacityProperty));
        _notificationPulse.Children.Add(pulse);

        Loaded += MainWindow_Loaded;
        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;
        MouseMove += MainWindow_MouseMove;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Start();
        _systemObserver.Start();
        _powerOptimizationHelper.Start(new WindowInteropHelper(this).Handle);
        await _mediaViewModel.InitializeAsync();
        await CheckForUpdatesAsync();
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        RepositionWindow();

        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        if (source is null)
        {
            throw new InvalidOperationException("Unable to access the window message source.");
        }

        source.AddHook(WindowMessageHook);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RepositionWindow();
    }

    private void RepositionWindow()
    {
        if (ActualWidth > 0)
        {
            Left = (SystemParameters.PrimaryScreenWidth - ActualWidth) / 2;
        }

        Top = 10;
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _viewModel.Dispose();
        _mediaViewModel.Dispose();
        PomodoroViewModel.PropertyChanged -= PomodoroViewModel_PropertyChanged;
        PomodoroViewModel.Dispose();
        _systemObserver.SystemEventDetected -= SystemObserver_SystemEventDetected;
        _systemObserver.Dispose();
        _powerOptimizationHelper.AnimationPauseChanged -= PowerOptimizationHelper_AnimationPauseChanged;
        _powerOptimizationHelper.Dispose();
        _notificationTimer.Stop();
        _notificationTimer.Tick -= NotificationTimer_Tick;
        if (_notificationPulseActive)
        {
            _notificationPulse.Stop(SystemNotificationBorder);
            _notificationPulseActive = false;
        }
    }

    private void MainWindow_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _viewModel.RecordMouseActivity();
    }

    private void IslandBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _viewModel.RecordMouseActivity();
        ClickThroughHelper.DisableClickThrough(this);
    }

    private void IslandBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        ClickThroughHelper.EnableClickThrough(this);
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WM_NCHITTEST)
        {
            return IntPtr.Zero;
        }

        var cursorPosition = System.Windows.Forms.Cursor.Position;
        var islandPoint = IslandBorder.PointFromScreen(
            new System.Windows.Point(cursorPosition.X, cursorPosition.Y));
        var islandWidth = IslandBorder.ActualWidth;
        var islandHeight = IslandBorder.ActualHeight;

        var isInsideIsland = islandPoint.X >= 0
            && islandPoint.Y >= 0
            && islandPoint.X < islandWidth
            && islandPoint.Y < islandHeight;

        if (isInsideIsland)
        {
            var cornerCenterX = islandPoint.X < IslandCornerRadius
                ? IslandCornerRadius
                : islandPoint.X >= islandWidth - IslandCornerRadius
                    ? islandWidth - IslandCornerRadius
                    : islandPoint.X;
            var cornerCenterY = islandPoint.Y < IslandCornerRadius
                ? IslandCornerRadius
                : islandPoint.Y >= islandHeight - IslandCornerRadius
                    ? islandHeight - IslandCornerRadius
                    : islandPoint.Y;
            var offsetX = islandPoint.X - cornerCenterX;
            var offsetY = islandPoint.Y - cornerCenterY;
            isInsideIsland = offsetX * offsetX + offsetY * offsetY
                <= IslandCornerRadius * IslandCornerRadius;
        }

        if (!isInsideIsland)
        {
            handled = true;
            return new IntPtr(HTTRANSPARENT);
        }

        handled = true;
        return new IntPtr(HTCLIENT);
    }

    private void Island_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && !IsMediaControl(e.OriginalSource as DependencyObject))
        {
            DragMove();
        }
    }

    private bool IsMediaControl(DependencyObject? source)
    {
        while (source is not null && !ReferenceEquals(source, IslandBorder))
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase)
            {
                return true;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void Island_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        System.Windows.Application.Current.Shutdown();
    }

    private void PomodoroViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PomodoroViewModel.IsFocusActive))
        {
            _viewModel.IsFocusActive = PomodoroViewModel.IsFocusActive;
        }
    }

    private void SystemObserver_SystemEventDetected(object? sender, string message)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _viewModel.SystemNotification = message;
            _viewModel.IsSystemNotificationVisible = true;
            ExpandIsland();
            StartNotificationPulse();
        }));
    }

    private void PowerOptimizationHelper_AnimationPauseChanged(object? sender, bool isPaused)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _viewModel.IsAnimationPaused = isPaused;
            if (isPaused)
            {
                if (_notificationPulseActive)
                {
                    _notificationPulse.Pause(SystemNotificationBorder);
                }
            }
            else if (_notificationPulseActive)
            {
                _notificationPulse.Resume(SystemNotificationBorder);
            }
        }));
    }

    private async Task CheckForUpdatesAsync()
    {
        var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
            ?? new Version(1, 0, 0);
        var checker = new UpdateChecker(UpdateChecker.DefaultVersionJsonUri, currentVersion);

        try
        {
            var update = await checker.CheckForUpdateAsync();
            if (update is null)
            {
                return;
            }

            _viewModel.UpdateDownloadUrl = update.DownloadUri.AbsoluteUri;
            _viewModel.UpdateNotification = $"Update v{update.Version} Tersedia! Klik untuk Update";
            _viewModel.IsUpdateAvailable = true;
            _normalWindowWidth = 520;
            Width = 520;
            RepositionWindow();
        }
        catch (HttpRequestException exception)
        {
            ShowSystemNotification($"Update check failed: {exception.Message}");
        }
        catch (TaskCanceledException exception)
        {
            ShowSystemNotification($"Update check timed out: {exception.Message}");
        }
        catch (JsonException exception)
        {
            ShowSystemNotification($"Update manifest invalid: {exception.Message}");
        }
        catch (InvalidDataException exception)
        {
            ShowSystemNotification($"Update manifest invalid: {exception.Message}");
        }
    }

    private void ExpandIsland()
    {
        Width = Math.Max(_normalWindowWidth, 500);
        RepositionWindow();
        _notificationTimer.Stop();
        _notificationTimer.Start();
    }

    private void ShowSystemNotification(string message)
    {
        _viewModel.SystemNotification = message;
        _viewModel.IsSystemNotificationVisible = true;
        ExpandIsland();
        StartNotificationPulse();
    }

    private void StartNotificationPulse()
    {
        _notificationPulse.Begin(SystemNotificationBorder, true);
        _notificationPulseActive = true;
        if (_powerOptimizationHelper.ShouldPauseAnimations)
        {
            _notificationPulse.Pause(SystemNotificationBorder);
        }
    }

    private void NotificationTimer_Tick(object? sender, EventArgs e)
    {
        _notificationTimer.Stop();
        _viewModel.IsSystemNotificationVisible = false;
        _notificationPulse.Stop(SystemNotificationBorder);
        _notificationPulseActive = false;
        if (!_viewModel.IsUpdateAvailable)
        {
            Width = _normalWindowWidth;
            RepositionWindow();
        }
    }

    private void IslandBorder_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        UpdateDropState(e);
    }

    private void IslandBorder_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        UpdateDropState(e);
    }

    private void IslandBorder_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        DropViewModel.SetDropZoneActive(false);
        _viewModel.IsDropTargetActive = false;
    }

    private void IslandBorder_Drop(object sender, System.Windows.DragEventArgs e)
    {
        var filePaths = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
        DropViewModel.HandleDrop(filePaths);
        _viewModel.IsDropTargetActive = false;
        e.Effects = DropViewModel.CanAcceptFile(filePaths)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void UpdateDropState(System.Windows.DragEventArgs e)
    {
        var filePaths = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
        var canAccept = DropViewModel.CanAcceptFile(filePaths);
        var isDraggingFiles = filePaths is { Length: > 0 };
        DropViewModel.SetDropZoneActive(isDraggingFiles);
        _viewModel.IsDropTargetActive = isDraggingFiles;
        _viewModel.RecordMouseActivity();
        e.Effects = canAccept
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }
}