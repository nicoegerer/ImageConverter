using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ImageConverter.Core.Models;
using ImageMagick;

namespace ImageConverter.Core.Services;

public sealed class ImageLoaderService(AppPaths paths, ILoggingService logger)
{
    private sealed record CachedMetadata(uint Width, uint Height, string Format, DateTime? CaptureTime)
    {
        public ImageFileInfo ToFileInfo(string path, string name, long bytes, DateTime created, DateTime modified, string thumbnail, string preview)
        {
            return new ImageFileInfo(path, name, Width, Height, Format, bytes, created, modified, CaptureTime, thumbnail, preview);
        }
    }

    public static IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif", ".avif", ".tiff", ".tif", ".bmp",
        ".gif", ".jxl"
    };

    public bool IsSupportedPath(string path)
    {
        return SupportedExtensions.Contains(Path.GetExtension(path));
    }

    public Task<ImageFileInfo> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Load(path, cancellationToken), cancellationToken);
    }

    private ImageFileInfo Load(string path, CancellationToken cancellationToken)
    {
        path = Path.GetFullPath(path);
        FileInfo fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Image file was not found.", path);
        }
        if (!IsSupportedPath(path))
        {
            throw new NotSupportedException("The extension '" + fileInfo.Extension + "' is not supported.");
        }
        string s = $"{path}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}";
        string text = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
        string text2 = Path.Combine(paths.Cache, text + ".thumb.png");
        string text3 = Path.Combine(paths.Cache, text + ".preview.png");
        string path2 = Path.Combine(paths.Cache, text + ".json");
        if (File.Exists(text2) && File.Exists(text3) && File.Exists(path2))
        {
            CachedMetadata? cachedMetadata = JsonSerializer.Deserialize<CachedMetadata>(File.ReadAllText(path2));
            if (cachedMetadata is not null)
            {
                return cachedMetadata.ToFileInfo(path, fileInfo.Name, fileInfo.Length, fileInfo.CreationTime, fileInfo.LastWriteTime, text2, text3);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using MagickImage magickImage = new MagickImage(path);
            magickImage.AutoOrient();
            cancellationToken.ThrowIfCancellationRequested();
            DateTime? captureTime = ReadCaptureTime(magickImage);
            using (MagickImage magickImage2 = (MagickImage)magickImage.Clone())
            {
                ResizeContained(magickImage2, 176, 112, allowUpscale: false);
                magickImage2.Format = MagickFormat.Png;
                magickImage2.Write(text2);
            }
            using (MagickImage magickImage3 = (MagickImage)magickImage.Clone())
            {
                ResizeContained(magickImage3, 2048, 2048, allowUpscale: false);
                magickImage3.Format = MagickFormat.Png;
                magickImage3.Write(text3);
            }
            CachedMetadata cachedMetadata2 = new CachedMetadata(magickImage.Width, magickImage.Height, magickImage.Format.ToString(), captureTime);
            File.WriteAllText(path2, JsonSerializer.Serialize(cachedMetadata2));
            return cachedMetadata2.ToFileInfo(path, fileInfo.Name, fileInfo.Length, fileInfo.CreationTime, fileInfo.LastWriteTime, text2, text3);
        }
        catch (Exception ex)
        {
            logger.Error("Image.Load", ex, path);
            throw new InvalidOperationException(fileInfo.Name + " could not be processed: " + ex.Message, ex);
        }
    }

    internal static void ResizeContained(MagickImage image, int maxWidth, int maxHeight, bool allowUpscale)
    {
        double num = Math.Min((double)maxWidth / (double)image.Width, (double)maxHeight / (double)image.Height);
        if (!allowUpscale)
        {
            num = Math.Min(num, 1.0);
        }
        uint num2 = Math.Max(1u, (uint)Math.Round((double)image.Width * num));
        uint num3 = Math.Max(1u, (uint)Math.Round((double)image.Height * num));
        if (num2 != image.Width || num3 != image.Height)
        {
            image.FilterType = FilterType.Lanczos;
            image.Resize(num2, num3);
        }
    }

    private static DateTime? ReadCaptureTime(MagickImage image)
    {
        if (!DateTime.TryParseExact(image.GetAttribute("exif:DateTimeOriginal") ?? image.GetAttribute("exif:DateTimeDigitized"), "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var result))
        {
            return null;
        }
        return result;
    }
}
