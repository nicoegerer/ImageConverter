namespace ImageConverter.Core.Models;

public sealed record RenamePlanItem(string SourcePath, string TemporaryPath, string DestinationPath, int Order);
