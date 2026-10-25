using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Deltha.Services;

public enum ImageConversionFormat
{
    Png,
    Jpg,
    Pdf
}

public sealed class ConverterService
{
    private const double PdfPageWidth = 595.28;
    private const double PdfPageHeight = 841.89;

    public async Task<string> ConvertAsync(
        string sourcePath,
        ImageConversionFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ValidateImageFile(sourcePath);

        var outputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        Directory.CreateDirectory(outputDirectory);

        var sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = format switch
        {
            ImageConversionFormat.Png => ".png",
            ImageConversionFormat.Jpg => ".jpg",
            ImageConversionFormat.Pdf => ".pdf",
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        var outputPath = GetUniqueOutputPath(outputDirectory, sourceName, extension);

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = LoadImage(sourcePath);

            if (format == ImageConversionFormat.Pdf)
            {
                var jpeg = EncodeJpeg(frame);
                cancellationToken.ThrowIfCancellationRequested();
                File.WriteAllBytes(outputPath, CreatePdf(jpeg, frame.PixelWidth, frame.PixelHeight));
                return;
            }

            var encoder = format == ImageConversionFormat.Png
                ? (BitmapEncoder)new PngBitmapEncoder()
                : new JpegBitmapEncoder { QualityLevel = 92 };
            encoder.Frames.Add(BitmapFrame.Create(
                format == ImageConversionFormat.Jpg ? FlattenOnWhite(frame) : frame));

            using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            encoder.Save(output);
        }, cancellationToken);

        return outputPath;
    }

    public static bool IsSupportedImage(string? path)
    {
        var extension = Path.GetExtension(path);
        return extension is not null && (extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateImageFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File gambar tidak ditemukan.", path);
        }

        if (!IsSupportedImage(path))
        {
            throw new NotSupportedException("Format yang didukung: PNG, JPG, WEBP, dan BMP.");
        }
    }

    private static BitmapFrame LoadImage(string path)
    {
        var decoder = BitmapDecoder.Create(
            new Uri(Path.GetFullPath(path)),
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    private static BitmapSource FlattenOnWhite(BitmapSource source)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
            context.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
        }

        var flattened = new RenderTargetBitmap(
            source.PixelWidth,
            source.PixelHeight,
            source.DpiX,
            source.DpiY,
            PixelFormats.Pbgra32);
        flattened.Render(visual);
        flattened.Freeze();
        return flattened;
    }

    private static byte[] EncodeJpeg(BitmapSource source)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
        encoder.Frames.Add(BitmapFrame.Create(FlattenOnWhite(source)));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static byte[] CreatePdf(byte[] jpeg, int imageWidth, int imageHeight)
    {
        var scale = Math.Min(PdfPageWidth / imageWidth, PdfPageHeight / imageHeight);
        var drawWidth = imageWidth * scale;
        var drawHeight = imageHeight * scale;
        var offsetX = (PdfPageWidth - drawWidth) / 2;
        var offsetY = (PdfPageHeight - drawHeight) / 2;
        var content = string.Format(
            CultureInfo.InvariantCulture,
            "q {0:0.###} 0 0 {1:0.###} {2:0.###} {3:0.###} cm /Im0 Do Q",
            drawWidth,
            drawHeight,
            offsetX,
            offsetY);
        var contentBytes = Encoding.ASCII.GetBytes(content);

        using var pdf = new MemoryStream();
        var offsets = new List<long> { 0 };

        void WriteObject(int number, byte[] bytes)
        {
            offsets.Add(pdf.Position);
            WriteAscii(pdf, $"{number} 0 obj\n");
            pdf.Write(bytes);
            WriteAscii(pdf, "\nendobj\n");
        }

        WriteAscii(pdf, "%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
        WriteObject(1, Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"));
        WriteObject(2, Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"));
        WriteObject(3, Encoding.ASCII.GetBytes(
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PdfPageWidth.ToString("0.##", CultureInfo.InvariantCulture)} {PdfPageHeight.ToString("0.##", CultureInfo.InvariantCulture)}] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>"));

        offsets.Add(pdf.Position);
        WriteAscii(pdf,
            $"4 0 obj\n<< /Type /XObject /Subtype /Image /Width {imageWidth} /Height {imageHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
        pdf.Write(jpeg);
        WriteAscii(pdf, "\nendstream\nendobj\n");

        offsets.Add(pdf.Position);
        WriteAscii(pdf, $"5 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
        pdf.Write(contentBytes);
        WriteAscii(pdf, "\nendstream\nendobj\n");

        var xrefOffset = pdf.Position;
        WriteAscii(pdf, $"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            WriteAscii(pdf, $"{offset:0000000000} 00000 n \n");
        }

        WriteAscii(pdf,
            $"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
        return pdf.ToArray();
    }

    private static string GetUniqueOutputPath(string directory, string name, string extension)
    {
        var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        var baseName = $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}";
        var path = Path.Combine(directory, baseName + extension);
        var suffix = 1;

        while (File.Exists(path))
        {
            path = Path.Combine(directory, $"{baseName}_{suffix++}{extension}");
        }

        return path;
    }

    private static void WriteAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}
