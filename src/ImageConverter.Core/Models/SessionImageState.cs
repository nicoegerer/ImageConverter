namespace ImageConverter.Core.Models;

public sealed record SessionImageState
{
    public string Path { get; init; } = string.Empty;

    public CropState Crop { get; init; } = CropState.Full;

    public bool IsSelected { get; init; } = true;

    public string? LastExportText { get; init; }
}
