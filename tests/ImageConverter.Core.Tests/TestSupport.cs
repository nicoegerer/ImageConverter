using ImageConverter.Core.Models;
using ImageConverter.Core.Services;
using ImageMagick;

namespace ImageConverter.Core.Tests;

internal sealed class TestWorkspace : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "ImageConverter.Tests", Guid.NewGuid().ToString("N"));

    public TestWorkspace()
    {
        Directory.CreateDirectory(Root);
    }

    public string PathFor(string fileName) => Path.Combine(Root, fileName);

    public ImageFileInfo CreateImage(string fileName, uint width = 1200, uint height = 800, MagickFormat format = MagickFormat.Jpeg)
    {
        string path = PathFor(fileName);
        using var image = new MagickImage(new MagickColor("#4B77BE"), width, height)
        {
            Format = format,
            Quality = 90
        };
        image.Write(path);
        var file = new FileInfo(path);
        return new ImageFileInfo(path, file.Name, width, height, format.ToString(), file.Length,
            file.CreationTime, file.LastWriteTime, null, path, path);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

internal sealed class TestLogger : ILoggingService
{
    public List<(string Operation, Exception Exception)> Errors { get; } = [];

    public void Info(string operation, string message, string? filePath = null)
    {
    }

    public void Error(string operation, Exception exception, string? filePath = null, string? message = null)
    {
        Errors.Add((operation, exception));
    }
}
