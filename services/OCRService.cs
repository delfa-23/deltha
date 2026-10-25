using System.IO;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace Deltha.Services;

public sealed class OCRService
{
    public async Task<string> ExtractTextAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("File gambar tidak ditemukan.", imagePath);
        }

        if (!ConverterService.IsSupportedImage(imagePath))
        {
            throw new NotSupportedException("OCR mendukung gambar PNG, JPG, WEBP, dan BMP.");
        }

        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? throw new InvalidOperationException(
                "OCR tidak tersedia. Pastikan bahasa OCR Windows sudah terpasang.");

        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(imagePath));
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var bitmap = await decoder.GetSoftwareBitmapAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var result = await engine.RecognizeAsync(bitmap);
        cancellationToken.ThrowIfCancellationRequested();
        return string.Join(
            Environment.NewLine,
            result.Lines.Select(line => line.Text));
    }
}
