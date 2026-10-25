using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Deltha.Services;

namespace Deltha.ViewModels;

public partial class FileDropViewModel : ObservableObject
{
    private readonly ConverterService _converterService;
    private readonly OCRService _ocrService;
    private string? _imagePath;

    [ObservableProperty]
    private bool _isDropZoneActive;

    [ObservableProperty]
    private bool _isImageReady;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _dropZoneMessage = "Lepaskan gambar untuk konversi atau OCR";

    public bool IsNotBusy => !IsBusy;

    public FileDropViewModel(
        ConverterService? converterService = null,
        OCRService? ocrService = null)
    {
        _converterService = converterService ?? new ConverterService();
        _ocrService = ocrService ?? new OCRService();
    }

    public void SetDropZoneActive(bool active)
    {
        IsDropZoneActive = active;
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    public bool CanAcceptFile(string[]? paths) =>
        paths is { Length: > 0 } && ConverterService.IsSupportedImage(paths[0]);

    public void HandleDrop(string[]? paths)
    {
        IsDropZoneActive = false;
        if (paths is not { Length: > 0 })
        {
            StatusMessage = "Tidak ada file gambar yang diterima.";
            return;
        }

        if (!ConverterService.IsSupportedImage(paths[0]))
        {
            IsImageReady = false;
            _imagePath = null;
            StatusMessage = "Format tidak didukung. Gunakan PNG, JPG, WEBP, atau BMP.";
            return;
        }

        if (!File.Exists(paths[0]))
        {
            IsImageReady = false;
            _imagePath = null;
            StatusMessage = "File gambar tidak ditemukan.";
            return;
        }

        _imagePath = paths[0];
        IsImageReady = true;
        StatusMessage = $"Gambar siap: {Path.GetFileName(paths[0])}";
    }

    [RelayCommand]
    private Task ConvertToPngAsync() => ConvertAsync(ImageConversionFormat.Png);

    [RelayCommand]
    private Task ConvertToJpgAsync() => ConvertAsync(ImageConversionFormat.Jpg);

    [RelayCommand]
    private Task ConvertToPdfAsync() => ConvertAsync(ImageConversionFormat.Pdf);

    [RelayCommand]
    private async Task ExtractTextAsync()
    {
        if (_imagePath is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            StatusMessage = "Mengekstrak teks...";
            var text = await _ocrService.ExtractTextAsync(_imagePath);
            if (string.IsNullOrWhiteSpace(text))
            {
                StatusMessage = "Tidak ada teks yang terdeteksi.";
                return;
            }

            System.Windows.Clipboard.SetText(text);
            StatusMessage = "Teks Berhasil Disalin!";
        }
        catch (Exception exception)
        {
            StatusMessage = $"OCR gagal: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ConvertAsync(ImageConversionFormat format)
    {
        if (_imagePath is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            StatusMessage = "Mengonversi gambar...";
            var outputPath = await _converterService.ConvertAsync(_imagePath, format);
            StatusMessage = $"File disimpan: {Path.GetFileName(outputPath)}";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Konversi gagal: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
