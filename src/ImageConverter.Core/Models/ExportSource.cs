namespace ImageConverter.Core.Models;

public sealed record ExportSource(ImageFileInfo File, CropState Crop, int Order);
