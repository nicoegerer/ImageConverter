using System;
using System.Collections.Generic;

namespace ImageConverter.Core.Models;

public sealed record ExportProfile
{
    public string Name { get; init; } = "Portfolio Card";

    public TransformationMode Mode { get; init; }

    public int Width { get; init; } = 600;

    public int Height { get; init; } = 1050;

    public OutputImageFormat Format { get; init; }

    public int Quality { get; init; } = 95;

    public bool PreventUpscale { get; init; }

    public bool PreserveAspectRatio { get; init; } = true;

    public MetadataMode Metadata { get; init; } = MetadataMode.RemoveExif;

    public TransparencyMode Transparency { get; init; } = TransparencyMode.Preserve;

    public static IReadOnlyList<ExportProfile> BuiltIn { get; } = new ExportProfile[5]
    {
        new ExportProfile(),
        new ExportProfile
        {
            Name = "Web Hero",
            Width = 1920,
            Height = 1080,
            Format = OutputImageFormat.WebP,
            Quality = 90,
            PreventUpscale = true,
            Metadata = MetadataMode.RemoveGps
        },
        new ExportProfile
        {
            Name = "Originalgröße · WebP klein",
            Mode = TransformationMode.OriginalSize,
            Width = 0,
            Height = 0,
            Format = OutputImageFormat.WebP,
            Quality = 82,
            Metadata = MetadataMode.RemoveAll
        },
        new ExportProfile
        {
            Name = "Instagram Square",
            Width = 1080,
            Height = 1080,
            Format = OutputImageFormat.WebP,
            Quality = 90,
            PreventUpscale = true,
            Metadata = MetadataMode.RemoveGps
        },
        new ExportProfile
        {
            Name = "Full HD",
            Width = 1920,
            Height = 1080,
            Format = OutputImageFormat.WebP,
            Quality = 90,
            PreventUpscale = true,
            Metadata = MetadataMode.RemoveGps
        }
    };

    public void Validate()
    {
        if (Quality is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Quality), "Quality must be between 1 and 100.");
        }

        if (Mode is not TransformationMode.FormatOnly and not TransformationMode.OriginalSize
            && (Width <= 0 || Height <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(Width), "Width and height must be positive.");
        }
    }

}
