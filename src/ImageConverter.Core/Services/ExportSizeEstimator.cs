using System;
using ImageConverter.Core.Models;

namespace ImageConverter.Core.Services;

public static class ExportSizeEstimator
{
    private sealed record GeometryEstimate(int OutputWidth, int OutputHeight, bool IsUpscaling, double UpscaleFactor);

    public static ExportSizeEstimate Estimate(ImageFileInfo source, ExportProfile profile, CropState crop)
    {
        GeometryEstimate geometry = EstimateGeometry(source, profile, crop);
        double sourcePixels = Math.Max(1.0, (double)source.Width * source.Height);
        double outputPixels = Math.Max(1.0, (double)geometry.OutputWidth * geometry.OutputHeight);
        double pixelScale = Math.Pow(outputPixels / sourcePixels, 0.82);
        double compressionScale = OutputCompressionFactor(profile.Format, profile.Quality) / SourceCompressionFactor(source.Format);
        double baseline = Math.Max(512.0, source.FileSize * pixelScale * compressionScale);
        double lowerFactor = geometry.IsUpscaling ? 0.5 : 0.65;
        double upperFactor = geometry.IsUpscaling ? 1.8 : 1.45;
        if (profile.Format == OutputImageFormat.Png)
        {
            lowerFactor = 0.5;
            upperFactor = 2.0;
        }

        return new ExportSizeEstimate(
            Math.Max(256L, (long)Math.Round(baseline * lowerFactor)),
            Math.Max(512L, (long)Math.Round(baseline * upperFactor)),
            geometry.OutputWidth,
            geometry.OutputHeight,
            geometry.IsUpscaling,
            geometry.UpscaleFactor);
    }

    private static GeometryEstimate EstimateGeometry(ImageFileInfo source, ExportProfile profile, CropState crop)
    {
        double sourceWidth = Math.Max(1.0, source.Width);
        double sourceHeight = Math.Max(1.0, source.Height);
        double effectiveWidth = sourceWidth;
        double effectiveHeight = sourceHeight;
        if (profile.Mode == TransformationMode.CropCover)
        {
            CropState clampedCrop = crop.Clamp();
            effectiveWidth = Math.Max(1.0, sourceWidth * clampedCrop.Width);
            effectiveHeight = Math.Max(1.0, sourceHeight * clampedCrop.Height);
        }

        double outputWidth;
        double outputHeight;
        switch (profile.Mode)
        {
            case TransformationMode.FormatOnly:
            case TransformationMode.OriginalSize:
                outputWidth = sourceWidth;
                outputHeight = sourceHeight;
                break;
            case TransformationMode.CropCover:
                if (profile.PreventUpscale && effectiveWidth <= profile.Width && effectiveHeight <= profile.Height)
                {
                    outputWidth = effectiveWidth;
                    outputHeight = effectiveHeight;
                }
                else
                {
                    outputWidth = profile.Width;
                    outputHeight = profile.Height;
                }
                break;
            case TransformationMode.ResizeStretch:
                outputWidth = profile.PreventUpscale ? Math.Min(profile.Width, sourceWidth) : profile.Width;
                outputHeight = profile.PreventUpscale ? Math.Min(profile.Height, sourceHeight) : profile.Height;
                break;
            case TransformationMode.ResizeKeepAspect:
            case TransformationMode.FitContain:
                {
                    double scale = Math.Min(profile.Width / sourceWidth, profile.Height / sourceHeight);
                    if (profile.PreventUpscale)
                    {
                        scale = Math.Min(1.0, scale);
                    }
                    outputWidth = Math.Max(1.0, sourceWidth * scale);
                    outputHeight = Math.Max(1.0, sourceHeight * scale);
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(profile.Mode));
        }

        double upscaleFactor = Math.Max(outputWidth / effectiveWidth, outputHeight / effectiveHeight);
        return new GeometryEstimate(
            Math.Max(1, (int)Math.Round(outputWidth)),
            Math.Max(1, (int)Math.Round(outputHeight)),
            upscaleFactor > 1.001,
            Math.Max(1.0, upscaleFactor));
    }

    private static double OutputCompressionFactor(OutputImageFormat format, int quality)
    {
        double x = (double)Math.Clamp(quality, 1, 100) / 100.0;
        return format switch
        {
            OutputImageFormat.WebP => 0.45 + 0.55 * Math.Pow(x, 1.7),
            OutputImageFormat.Jpeg => 0.55 + 0.7 * Math.Pow(x, 1.6),
            OutputImageFormat.Avif => 0.35 + 0.45 * Math.Pow(x, 1.7),
            OutputImageFormat.Png => 1.4,
            _ => 1.0,
        };
    }

    private static double SourceCompressionFactor(string format)
    {
        return format.Trim().ToLowerInvariant() switch
        {
            "heic" or "heif" or "avif" => 0.75,
            "webp" => 0.9,
            "png" => 1.5,
            "tif" or "tiff" => 2.0,
            "bmp" => 4.0,
            _ => 1.0
        };
    }
}
