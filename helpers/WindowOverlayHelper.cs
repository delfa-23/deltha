using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Deltha.Helpers;

internal static class WindowOverlayHelper
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public static void ToggleClickThrough(Window window, bool enabled)
    {
        if (window is null)
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var currentStyle = GetWindowLongPtr(handle, GWL_EXSTYLE);
        var updatedStyle = enabled
            ? currentStyle | (IntPtr)WS_EX_TRANSPARENT
            : currentStyle & ~(IntPtr)WS_EX_TRANSPARENT;

        SetWindowLongPtr(handle, GWL_EXSTYLE, updatedStyle);
        SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    public static void CenterTopOverlay(Window window)
    {
        var workingArea = SystemParameters.WorkArea;
        var width = window.Width;
        var height = window.Height;

        window.Left = workingArea.Left + ((workingArea.Width - width) / 2);
        window.Top = workingArea.Top;
    }
}
