using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Deltha.Services;

namespace Deltha.ViewModels;

public partial class LauncherViewModel : ObservableObject
{
    private readonly LauncherService _launcherService;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<LauncherItem> Applications { get; } = new();

    public LauncherViewModel(LauncherService? launcherService = null)
    {
        _launcherService = launcherService ?? new LauncherService();
        RefreshShortcuts();
    }

    public void RefreshShortcuts()
    {
        try
        {
            var shortcuts = _launcherService.GetStartMenuShortcuts();
            Applications.Clear();
            foreach (var shortcut in shortcuts)
            {
                Applications.Add(shortcut);
            }

            StatusMessage = shortcuts.Count == 0
                ? "Tidak ada shortcut Start Menu."
                : $"{shortcuts.Count} aplikasi tersedia.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Gagal memuat shortcut: {exception.Message}";
        }
    }

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    [RelayCommand]
    private void AddApplication()
    {
        try
        {
            var application = _launcherService.BrowseForExecutable();
            if (application is null)
            {
                return;
            }

            if (!Applications.Any(item => string.Equals(
                    item.Path,
                    application.Path,
                    StringComparison.OrdinalIgnoreCase)))
            {
                Applications.Add(application);
                StatusMessage = $"{application.Name} ditambahkan.";
            }
        }
        catch (Exception exception)
        {
            StatusMessage = $"Gagal memilih aplikasi: {exception.Message}";
        }
    }

    [RelayCommand]
    private void Launch(LauncherItem? application)
    {
        if (application is null)
        {
            return;
        }

        try
        {
            _launcherService.Launch(application);
            StatusMessage = $"Membuka {application.Name}...";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Gagal membuka {application.Name}: {exception.Message}";
        }
    }
}
