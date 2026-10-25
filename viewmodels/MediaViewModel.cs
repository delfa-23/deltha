using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Deltha.Services;

namespace Deltha.ViewModels;

public partial class MediaViewModel : ObservableObject, IDisposable
{
    private readonly MediaService _mediaService;
    private readonly LyricsService _lyricsService;
    private CancellationTokenSource? _lyricsRequest;
    private Task? _initializationTask;
    private bool _isInitialized;
    private bool _isDisposed;

    [ObservableProperty]
    private string _trackTitle = "Tidak Ada Media";

    [ObservableProperty]
    private string _artistName = "Deltha Music";

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string _lyricsText = "Buka panel untuk mencari lirik.";

    [ObservableProperty]
    private bool _isLyricsExpanded;

    public MediaViewModel(MediaService? mediaService = null, LyricsService? lyricsService = null)
    {
        _mediaService = mediaService ?? new MediaService();
        _lyricsService = lyricsService ?? new LyricsService();
    }

    public Task InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        return _isInitialized
            ? Task.CompletedTask
            : _initializationTask ??= InitializeCoreAsync();
    }

    private async Task InitializeCoreAsync()
    {
        _mediaService.CurrentMediaChanged += MediaService_CurrentMediaChanged;
        try
        {
            await _mediaService.InitializeAsync();
            _isInitialized = true;
        }
        finally
        {
            if (!_isInitialized)
            {
                _mediaService.CurrentMediaChanged -= MediaService_CurrentMediaChanged;
                _initializationTask = null;
            }
        }
    }

    [RelayCommand]
    private async Task TogglePlayPauseAsync()
    {
        await InitializeAsync();
        await _mediaService.TogglePlayPauseAsync();
        await RefreshMediaAsync();
    }

    [RelayCommand]
    private async Task NextTrackAsync()
    {
        await InitializeAsync();
        await _mediaService.NextAsync();
        await RefreshMediaAsync();
    }

    [RelayCommand]
    private async Task PreviousTrackAsync()
    {
        await InitializeAsync();
        await _mediaService.PreviousAsync();
        await RefreshMediaAsync();
    }

    [RelayCommand]
    private async Task ToggleLyricsAsync()
    {
        IsLyricsExpanded = !IsLyricsExpanded;

        if (IsLyricsExpanded)
        {
            await InitializeAsync();
            await FetchLyricsAsync();
        }
        else
        {
            CancelLyricsRequest();
        }
    }

    public async Task RefreshMediaAsync()
    {
        var mediaInfo = await _mediaService.GetCurrentSessionAsync();
        if (mediaInfo is not null)
        {
            ApplyMediaInfo(mediaInfo);
        }
    }

    public async Task FetchLyricsAsync()
    {
        if (string.Equals(TrackTitle, "Tidak Ada Media", StringComparison.OrdinalIgnoreCase))
        {
            LyricsText = "Lirik tidak tersedia untuk media saat ini.";
            return;
        }

        CancelLyricsRequest();
        var request = new CancellationTokenSource();
        _lyricsRequest = request;
        LyricsText = "Mencari lirik...";

        try
        {
            var lyrics = await _lyricsService.GetLyricsAsync(
                TrackTitle,
                ArtistName,
                request.Token);

            if (!request.IsCancellationRequested)
            {
                LyricsText = lyrics;
            }
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_lyricsRequest, request))
            {
                _lyricsRequest = null;
            }

            request.Dispose();
        }
    }

    private void MediaService_CurrentMediaChanged(object? sender, MediaSessionInfo mediaInfo)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ApplyMediaInfo(mediaInfo);
            return;
        }

        _ = dispatcher.InvokeAsync(() => ApplyMediaInfo(mediaInfo));
    }

    private void ApplyMediaInfo(MediaSessionInfo mediaInfo)
    {
        var trackChanged = TrackTitle != mediaInfo.Title || ArtistName != mediaInfo.Artist;
        TrackTitle = mediaInfo.Title;
        ArtistName = mediaInfo.Artist;
        IsPlaying = mediaInfo.IsPlaying;

        if (trackChanged && IsLyricsExpanded)
        {
            _ = FetchLyricsAsync();
        }
    }

    private void CancelLyricsRequest()
    {
        _lyricsRequest?.Cancel();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _mediaService.CurrentMediaChanged -= MediaService_CurrentMediaChanged;
        _mediaService.Dispose();
        CancelLyricsRequest();
        _lyricsRequest = null;
    }
}
