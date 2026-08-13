using System;
using System.Collections.Generic;
using System.Linq;
using ImageMagick;

namespace ImageConverter.Core.Services;

public sealed class CapabilityService
{
    private static readonly string[] RequiredFormats = ["JPEG", "PNG", "WEBP", "HEIC", "HEIF", "AVIF", "TIFF", "BMP"];

    public IReadOnlyList<FormatCapability> GetRequiredCapabilities()
    {
        Dictionary<string, IMagickFormatInfo> supported = MagickNET.SupportedFormats
            .ToDictionary(format => format.Format.ToString(), StringComparer.OrdinalIgnoreCase);

        return RequiredFormats.Select(name => supported.TryGetValue(name, out IMagickFormatInfo? format)
            ? new FormatCapability(name, format.SupportsReading, format.SupportsWriting)
            : new FormatCapability(name, CanRead: false, CanWrite: false)).ToArray();
    }

    public void EnsureReleaseCapabilities()
    {
        IReadOnlyList<FormatCapability> capabilities = GetRequiredCapabilities();
        string[] missingReaders = capabilities
            .Where(x => x.Name is "HEIC" or "HEIF" && !x.CanRead)
            .Select(x => x.Name)
            .ToArray();
        string[] missingWriters = capabilities
            .Where(x => x.Name is "JPEG" or "PNG" or "WEBP" or "AVIF" && !x.CanWrite)
            .Select(x => x.Name)
            .ToArray();

        if (missingReaders.Length != 0 || missingWriters.Length != 0)
        {
            throw new InvalidOperationException(
                $"Missing image delegates. Readers: {string.Join(", ", missingReaders)}; Writers: {string.Join(", ", missingWriters)}");
        }
    }
}
