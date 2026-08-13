using System;
using System.Collections.Generic;

namespace ImageConverter.Core.Models;

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 2;

    public string Theme { get; init; } = "System";

    public string? LastSourceFolder { get; init; }

    public string? LastDestinationFolder { get; init; }

    public OutputImageFormat LastOutputFormat { get; init; }

    public TransformationMode LastTransformationMode { get; init; } = TransformationMode.OriginalSize;

    public TransparencyMode LastTransparencyMode { get; init; } = TransparencyMode.Preserve;

    public MetadataMode LastMetadataMode { get; init; } = MetadataMode.RemoveAll;

    public int LastQuality { get; init; } = 95;

    public int LastWidth { get; init; } = 600;

    public int LastHeight { get; init; } = 1050;

    public string NamingPrefix { get; init; } = "Bild";

    public int NamingStartNumber { get; init; } = 1;

    public int NamingNumberLength { get; init; } = 1;

    public int ExportParallelism { get; init; } = 2;

    public bool RestoreLastSession { get; init; } = true;

    public bool CheckForUpdates { get; init; } = true;

    public DateTimeOffset? LastUpdateCheckUtc { get; init; }

    public string? SelectedImagePath { get; init; }

    public List<SessionImageState> SessionImages { get; init; } = [];
}
