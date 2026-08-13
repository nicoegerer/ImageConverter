namespace ImageConverter.Core.Models;

public sealed record ExportResult(ExportJob Job, bool Success, long OutputBytes, uint OutputWidth, uint OutputHeight, string? ErrorMessage, string? ErrorDetails);
