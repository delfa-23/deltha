namespace Deltha.Models;

public enum OverlayDisplayMode
{
    PrimaryScreenOnly,
    FollowMouseActiveScreen
}

public sealed class AppSettings
{
    public OverlayDisplayMode DisplayMode { get; set; } = OverlayDisplayMode.PrimaryScreenOnly;
    public bool IsClickThroughEnabled { get; set; } = true;
    public double OverlayOpacity { get; set; } = 1.0;
    public bool IsBatterySaverMode { get; set; } = false;
    public bool IsFullScreenSessionDetected { get; set; } = false;
}
