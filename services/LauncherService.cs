using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Deltha.Services;

public sealed record LauncherItem(string Name, string Path, ImageSource? Icon);

public sealed class LauncherService
{
    private const uint ShgfiIcon = 0x000000100;

    public IReadOnlyList<LauncherItem> GetStartMenuShortcuts()
    {
        var programsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var commonProgramsFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
        var folders = new[] { programsFolder, commonProgramsFolder }
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return folders
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(CreateLauncherItem)
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public LauncherItem? BrowseForExecutable()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Pilih aplikasi",
            Filter = "Aplikasi (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true
            ? CreateLauncherItem(dialog.FileName)
            : null;
    }

    public void Launch(LauncherItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!File.Exists(item.Path))
        {
            throw new FileNotFoundException("Shortcut atau aplikasi tidak ditemukan.", item.Path);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = item.Path,
            UseShellExecute = true
        });
    }

    private static LauncherItem CreateLauncherItem(string path) =>
        new(Path.GetFileNameWithoutExtension(path), path, TryGetIcon(path));

    private static ImageSource? TryGetIcon(string path)
    {
        var info = new ShellFileInfo();
        var result = SHGetFileInfo(
            path,
            0,
            ref info,
            (uint)Marshal.SizeOf<ShellFileInfo>(),
            ShgfiIcon);
        if (result == IntPtr.Zero || info.Icon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                info.Icon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(24, 24));
            source.Freeze();
            return source;
        }
        finally
        {
            DestroyIcon(info.Icon);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public IntPtr Icon;
        public int IconIndex;
        public uint Attributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string? DisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string? TypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string path,
        uint fileAttributes,
        ref ShellFileInfo fileInfo,
        uint fileInfoSize,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr icon);
}
