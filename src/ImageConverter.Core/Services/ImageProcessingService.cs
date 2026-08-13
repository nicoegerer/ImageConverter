using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageConverter.Core.Models;
using ImageMagick;

namespace ImageConverter.Core.Services;

public sealed class ImageProcessingService(ILoggingService logger)
{
    public Task<ExportResult> ProcessAsync(ExportJob job, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Process(job, cancellationToken), cancellationToken);
    }

    private ExportResult Process(ExportJob job, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using MagickImage magickImage = new MagickImage(job.Source.File.Path);
            magickImage.AutoOrient();
            magickImage.Orientation = OrientationType.TopLeft;
            ApplyGeometry(magickImage, job.Profile, job.Source.Crop);
            ApplyMetadata(magickImage, job.Profile.Metadata);
            magickImage.Quality = (uint)job.Profile.Quality;
            magickImage.Format = GetMagickFormat(job.Profile.Format);
            cancellationToken.ThrowIfCancellationRequested();
            magickImage.Write(job.TemporaryPath);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(job.TemporaryPath, job.DestinationPath, overwrite: false);
            long length = new FileInfo(job.DestinationPath).Length;
            return new ExportResult(job, Success: true, length, magickImage.Width, magickImage.Height, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryDelete(job.TemporaryPath);
            throw;
        }
        catch (Exception ex2)
        {
            TryDelete(job.TemporaryPath);
            logger.Error("Export.Process", ex2, job.Source.File.Path);
            return new ExportResult(job, Success: false, 0L, 0u, 0u, job.Source.File.FileName + " konnte nicht verarbeitet werden.", ex2.ToString());
        }
    }

    private static void ApplyGeometry(MagickImage image, ExportProfile profile, CropState crop)
    {
        switch (profile.Mode)
        {
            case TransformationMode.FormatOnly:
            case TransformationMode.OriginalSize:
                break;
            case TransformationMode.CropCover:
                {
                    CropState cropState = crop.Clamp();
                    int num = Math.Clamp((int)Math.Round(cropState.X * (double)image.Width), 0, (int)(image.Width - 1));
                    int num2 = Math.Clamp((int)Math.Round(cropState.Y * (double)image.Height), 0, (int)(image.Height - 1));
                    int width = Math.Clamp((int)Math.Round(cropState.Width * (double)image.Width), 1, (int)image.Width - num);
                    int height = Math.Clamp((int)Math.Round(cropState.Height * (double)image.Height), 1, (int)image.Height - num2);
                    image.Crop(new MagickGeometry(num, num2, (uint)width, (uint)height));
                    image.ResetPage();
                    if (!profile.PreventUpscale || image.Width > profile.Width || image.Height > profile.Height)
                    {
                        image.FilterType = FilterType.Lanczos;
                        image.Resize(new MagickGeometry((uint)profile.Width, (uint)profile.Height)
                        {
                            IgnoreAspectRatio = true
                        });
                    }
                    break;
                }
            case TransformationMode.ResizeStretch:
                {
                    int num3 = (profile.PreventUpscale ? Math.Min(profile.Width, (int)image.Width) : profile.Width);
                    int num4 = (profile.PreventUpscale ? Math.Min(profile.Height, (int)image.Height) : profile.Height);
                    if (image.Width != num3 || image.Height != num4)
                    {
                        image.FilterType = FilterType.Lanczos;
                        image.Resize(new MagickGeometry((uint)num3, (uint)num4)
                        {
                            IgnoreAspectRatio = true
                        });
                    }
                    break;
                }
            case TransformationMode.ResizeKeepAspect:
            case TransformationMode.FitContain:
                if (!profile.PreserveAspectRatio)
                {
                    if (!profile.PreventUpscale || image.Width > profile.Width || image.Height > profile.Height)
                    {
                        image.FilterType = FilterType.Lanczos;
                        image.Resize((uint)profile.Width, (uint)profile.Height);
                    }
                }
                else
                {
                    ImageLoaderService.ResizeContained(image, profile.Width, profile.Height, !profile.PreventUpscale);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException("Mode");
        }
    }

    private static void ApplyMetadata(MagickImage image, MetadataMode mode)
    {
        switch (mode)
        {
            case MetadataMode.Keep:
                break;
            case MetadataMode.RemoveAll:
                image.Strip();
                break;
            case MetadataMode.RemoveExif:
                image.RemoveProfile("exif");
                break;
            case MetadataMode.RemoveGps:
                RemoveGpsTags(image);
                break;
            default:
                throw new ArgumentOutOfRangeException("mode");
        }
    }

    private static void RemoveGpsTags(MagickImage image)
    {
        IExifProfile? exifProfile = image.GetExifProfile();
        if (exifProfile != null)
        {
            exifProfile.RemoveValue(ExifTag.GPSAltitude);
            exifProfile.RemoveValue(ExifTag.GPSAltitudeRef);
            exifProfile.RemoveValue(ExifTag.GPSAreaInformation);
            exifProfile.RemoveValue(ExifTag.GPSDateStamp);
            exifProfile.RemoveValue(ExifTag.GPSDestBearing);
            exifProfile.RemoveValue(ExifTag.GPSDestBearingRef);
            exifProfile.RemoveValue(ExifTag.GPSDestDistance);
            exifProfile.RemoveValue(ExifTag.GPSDestDistanceRef);
            exifProfile.RemoveValue(ExifTag.GPSDestLatitude);
            exifProfile.RemoveValue(ExifTag.GPSDestLatitudeRef);
            exifProfile.RemoveValue(ExifTag.GPSDestLongitude);
            exifProfile.RemoveValue(ExifTag.GPSDestLongitudeRef);
            exifProfile.RemoveValue(ExifTag.GPSDifferential);
            exifProfile.RemoveValue(ExifTag.GPSDOP);
            exifProfile.RemoveValue(ExifTag.GPSImgDirection);
            exifProfile.RemoveValue(ExifTag.GPSImgDirectionRef);
            exifProfile.RemoveValue(ExifTag.GPSLatitude);
            exifProfile.RemoveValue(ExifTag.GPSLatitudeRef);
            exifProfile.RemoveValue(ExifTag.GPSLongitude);
            exifProfile.RemoveValue(ExifTag.GPSLongitudeRef);
            exifProfile.RemoveValue(ExifTag.GPSMapDatum);
            exifProfile.RemoveValue(ExifTag.GPSMeasureMode);
            exifProfile.RemoveValue(ExifTag.GPSProcessingMethod);
            exifProfile.RemoveValue(ExifTag.GPSSatellites);
            exifProfile.RemoveValue(ExifTag.GPSSpeed);
            exifProfile.RemoveValue(ExifTag.GPSSpeedRef);
            exifProfile.RemoveValue(ExifTag.GPSStatus);
            exifProfile.RemoveValue(ExifTag.GPSTrack);
            exifProfile.RemoveValue(ExifTag.GPSTrackRef);
            exifProfile.RemoveValue(ExifTag.GPSVersionID);
            image.SetProfile(exifProfile);
        }
    }

    private static MagickFormat GetMagickFormat(OutputImageFormat format)
    {
        return format switch
        {
            OutputImageFormat.WebP => MagickFormat.WebP,
            OutputImageFormat.Jpeg => MagickFormat.Jpeg,
            OutputImageFormat.Png => MagickFormat.Png,
            OutputImageFormat.Avif => MagickFormat.Avif,
            _ => throw new ArgumentOutOfRangeException("format"),
        };
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
