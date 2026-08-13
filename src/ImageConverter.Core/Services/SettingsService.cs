using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ImageConverter.Core.Models;

namespace ImageConverter.Core.Services;

public sealed class SettingsService(AppPaths paths, ILoggingService logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { (JsonConverter)new JsonStringEnumConverter() }
    };

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(paths.SettingsFile))
        {
            return new AppSettings();
        }
        try
        {
            AppSettings result;
            await using (FileStream stream = File.OpenRead(paths.SettingsFile))
            {
                result = Validate((await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)) ?? new AppSettings());
            }
            return result;
        }
        catch (Exception exception)
        {
            logger.Error("Settings.Load", exception, paths.SettingsFile);
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        settings = Validate(settings);
        string temporary = paths.SettingsFile + ".tmp";
        await using (FileStream stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        File.Move(temporary, paths.SettingsFile, overwrite: true);
    }

    private static AppSettings Validate(AppSettings settings)
    {
        string theme = settings.Theme is "Light" or "Dark" ? settings.Theme : "System";
        List<SessionImageState> sessionImages = (settings.SessionImages ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Path))
            .DistinctBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Take(500)
            .Select(item => item with { Crop = item.Crop.Clamp() })
            .ToList();
        return settings with
        {
            SchemaVersion = 2,
            Theme = theme,
            LastOutputFormat = Enum.IsDefined(settings.LastOutputFormat) ? settings.LastOutputFormat : OutputImageFormat.WebP,
            LastTransformationMode = Enum.IsDefined(settings.LastTransformationMode) ? settings.LastTransformationMode : TransformationMode.OriginalSize,
            LastTransparencyMode = Enum.IsDefined(settings.LastTransparencyMode) ? settings.LastTransparencyMode : TransparencyMode.Preserve,
            LastMetadataMode = Enum.IsDefined(settings.LastMetadataMode) ? settings.LastMetadataMode : MetadataMode.RemoveAll,
            LastQuality = Math.Clamp(settings.LastQuality, 1, 100),
            LastWidth = Math.Clamp(settings.LastWidth, 1, 32768),
            LastHeight = Math.Clamp(settings.LastHeight, 1, 32768),
            NamingStartNumber = Math.Max(0, settings.NamingStartNumber),
            NamingNumberLength = Math.Clamp(settings.NamingNumberLength, 1, 8),
            ExportParallelism = Math.Clamp(settings.ExportParallelism, 1, 4),
            SessionImages = sessionImages
        };
    }
}
