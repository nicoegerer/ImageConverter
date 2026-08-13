using System;

namespace ImageConverter.Core.Models;

public sealed record ImageFileInfo(string Path, string FileName, uint Width, uint Height, string Format, long FileSize, DateTime CreatedTime, DateTime ModifiedTime, DateTime? CaptureTime, string ThumbnailPath, string PreviewPath);
