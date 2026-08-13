namespace ImageConverter.Core.Models;

public sealed record ExportJob(ExportSource Source, ExportProfile Profile, string DestinationPath, string TemporaryPath);
