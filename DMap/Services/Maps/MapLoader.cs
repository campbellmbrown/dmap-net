using System;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Media.Imaging;

using PDFtoImage;

namespace DMap.Services.Maps;

/// <summary>
/// Default map loader for image files and first-page PDF rasterization.
/// </summary>
public sealed class MapLoader : IMapLoader
{
    const int PdfRenderDpi = 150;

    /// <inheritdoc/>
    public async Task<LoadedMap> LoadAsync(string path)
    {
        var encodedBytes = IsPdf(path)
            ? await RenderPdfFirstPageAsync(path)
            : await File.ReadAllBytesAsync(path);

        using var stream = new MemoryStream(encodedBytes);
        var image = new Bitmap(stream);
        return new LoadedMap(image, encodedBytes, image.PixelSize);
    }

    static bool IsPdf(string path) =>
        string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);

    static async Task<byte[]> RenderPdfFirstPageAsync(string path)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("PDF map loading is supported on Windows, Linux, and macOS.");

        var pdfBytes = await File.ReadAllBytesAsync(path);
        return await Task.Run(() =>
        {
            using var imageStream = new MemoryStream();
#pragma warning disable CA1416
            Conversion.SavePng(
                imageStream,
                pdfBytes,
                new Index(0),
                password: null,
                new RenderOptions
                {
                    Dpi = PdfRenderDpi,
                    UseTiling = true,
                });
#pragma warning restore CA1416

            return imageStream.ToArray();
        });
    }
}
