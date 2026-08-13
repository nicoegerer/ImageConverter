namespace ImageConverter.Core.Models;

public sealed record ExportSizeEstimate(long LowerBytes, long UpperBytes, int OutputWidth, int OutputHeight, bool IsUpscaling, double UpscaleFactor);
