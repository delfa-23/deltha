using Windows.Media.Control;

namespace Deltha.Services;

public sealed class MediaSessionInfo
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = "Tidak Ada Media";
    public string Artist { get; init; } = "Deltha Music";
    public bool IsPlaying { get; init; }
}

public sealed class MediaService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _selectedSession;
    private bool _isDisposed;

    public event EventHandler<MediaSessionInfo>? CurrentMediaChanged;

    public async Task InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_manager is null)
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += Manager_CurrentSessionChanged;
            _manager.SessionsChanged += Manager_SessionsChanged;
        }

        await SelectSessionAsync(_manager.GetCurrentSession()
            ?? _manager.GetSessions().FirstOrDefault());
    }

    public async Task<MediaSessionInfo?> GetCurrentSessionAsync()
    {
        await InitializeAsync();
        return _selectedSession is null
            ? null
            : await BuildSessionInfoAsync(_selectedSession);
    }

    public async Task TogglePlayPauseAsync()
    {
        var session = await ResolveSessionAsync();
        if (session is not null)
        {
            await session.TryTogglePlayPauseAsync();
            await PublishSessionAsync(session);
        }
    }

    public async Task NextAsync()
    {
        var session = await ResolveSessionAsync();
        if (session is not null)
        {
            await session.TrySkipNextAsync();
            await PublishSessionAsync(session);
        }
    }

    public async Task PreviousAsync()
    {
        var session = await ResolveSessionAsync();
        if (session is not null)
        {
            await session.TrySkipPreviousAsync();
            await PublishSessionAsync(session);
        }
    }

    private async Task<GlobalSystemMediaTransportControlsSession?> ResolveSessionAsync()
    {
        await InitializeAsync();
        return _selectedSession;
    }

    private async Task SelectSessionAsync(GlobalSystemMediaTransportControlsSession? session)
    {
        if (ReferenceEquals(_selectedSession, session))
        {
            if (session is not null)
            {
                await PublishSessionAsync(session);
            }
            else
            {
                CurrentMediaChanged?.Invoke(this, new MediaSessionInfo());
            }

            return;
        }

        if (_selectedSession is not null)
        {
            _selectedSession.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
            _selectedSession.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
        }

        _selectedSession = session;

        if (_selectedSession is null)
        {
            CurrentMediaChanged?.Invoke(this, new MediaSessionInfo());
            return;
        }

        _selectedSession.MediaPropertiesChanged += Session_MediaPropertiesChanged;
        _selectedSession.PlaybackInfoChanged += Session_PlaybackInfoChanged;
        await PublishSessionAsync(_selectedSession);
    }

    private async Task PublishSessionAsync(GlobalSystemMediaTransportControlsSession session)
    {
        var info = await BuildSessionInfoAsync(session);
        if (info is not null && ReferenceEquals(session, _selectedSession))
        {
            CurrentMediaChanged?.Invoke(this, info);
        }
    }

    private static async Task<MediaSessionInfo?> BuildSessionInfoAsync(
        GlobalSystemMediaTransportControlsSession session)
    {
        var properties = await session.TryGetMediaPropertiesAsync();
        var isPlaying = session.GetPlaybackInfo()?.PlaybackStatus
            == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        return new MediaSessionInfo
        {
            Id = session.SourceAppUserModelId,
            Title = string.IsNullOrWhiteSpace(properties?.Title)
                ? "Tidak Ada Media"
                : properties.Title,
            Artist = string.IsNullOrWhiteSpace(properties?.Artist)
                ? "Deltha Music"
                : properties.Artist,
            IsPlaying = isPlaying
        };
    }

    private async void Manager_CurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args)
    {
        await SelectSessionAsync(sender.GetCurrentSession()
            ?? sender.GetSessions().FirstOrDefault());
    }

    private async void Manager_SessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args)
    {
        await SelectSessionAsync(sender.GetCurrentSession()
            ?? sender.GetSessions().FirstOrDefault());
    }

    private async void Session_MediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args)
    {
        await PublishSessionAsync(sender);
    }

    private async void Session_PlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args)
    {
        await PublishSessionAsync(sender);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= Manager_CurrentSessionChanged;
            _manager.SessionsChanged -= Manager_SessionsChanged;
        }

        if (_selectedSession is not null)
        {
            _selectedSession.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
            _selectedSession.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
        }
    }
}
