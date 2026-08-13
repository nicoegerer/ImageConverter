using ImageConverter.Core.Models;
using ImageConverter.Core.Services;
using ImageMagick;

namespace ImageConverter.Core.Tests;

public sealed class ImageProcessingIntegrationTests
{
    [Theory]
    [InlineData(OutputImageFormat.WebP)]
    [InlineData(OutputImageFormat.Jpeg)]
    [InlineData(OutputImageFormat.Png)]
    [InlineData(OutputImageFormat.Avif)]
    [InlineData(OutputImageFormat.Tiff)]
    [InlineData(OutputImageFormat.Bmp)]
    [InlineData(OutputImageFormat.Gif)]
    public async Task CropCover_ExportsExactTargetSize(OutputImageFormat format)
    {
        using var workspace = new TestWorkspace();
        ImageFileInfo source = workspace.CreateImage("landscape.jpg", 1600, 900);
        var profile = new ExportProfile
        {
            Mode = TransformationMode.CropCover,
            Width = 600,
            Height = 1050,
            Format = format,
            Quality = 90,
            Metadata = MetadataMode.RemoveAll
        };
        ExportResult result = await ProcessAsync(workspace, source, profile,
            CropState.CreateCentered(1600d / 900d, 4d / 7d));

        Assert.True(result.Success, result.ErrorDetails);
        Assert.Equal(600u, result.OutputWidth);
        Assert.Equal(1050u, result.OutputHeight);
        using var exported = new MagickImage(result.Job.DestinationPath);
        Assert.Equal(600u, exported.Width);
        Assert.Equal(1050u, exported.Height);
    }

    [Fact]
    public async Task FormatOnly_KeepsOriginalPixelDimensions()
    {
        using var workspace = new TestWorkspace();
        ImageFileInfo source = workspace.CreateImage("original.jpg", 4032, 3024);
        var profile = new ExportProfile
        {
            Mode = TransformationMode.FormatOnly,
            Format = OutputImageFormat.WebP,
            Quality = 85
        };

        ExportResult result = await ProcessAsync(workspace, source, profile, CropState.Full);

        Assert.True(result.Success, result.ErrorDetails);
        Assert.Equal(4032u, result.OutputWidth);
        Assert.Equal(3024u, result.OutputHeight);
    }

    [Fact]
    public async Task ResizeKeepAspect_FitsWithinTargetWithoutDistortion()
    {
        using var workspace = new TestWorkspace();
        ImageFileInfo source = workspace.CreateImage("landscape.jpg", 4032, 3024);
        var profile = new ExportProfile
        {
            Mode = TransformationMode.ResizeKeepAspect,
            Width = 1600,
            Height = 1600,
            Format = OutputImageFormat.Jpeg,
            Quality = 90
        };

        ExportResult result = await ProcessAsync(workspace, source, profile, CropState.Full);

        Assert.True(result.Success, result.ErrorDetails);
        Assert.Equal(1600u, result.OutputWidth);
        Assert.Equal(1200u, result.OutputHeight);
    }

    [Fact]
    public async Task FitContain_KeepsCompleteImageWithinMaximumBounds()
    {
        using var workspace = new TestWorkspace();
        ImageFileInfo source = workspace.CreateImage("wide.jpg", 1600, 900);
        var profile = new ExportProfile
        {
            Mode = TransformationMode.FitContain,
            Width = 1200,
            Height = 1200,
            Format = OutputImageFormat.WebP
        };

        ExportResult result = await ProcessAsync(workspace, source, profile, CropState.Full);

        Assert.True(result.Success, result.ErrorDetails);
        Assert.Equal(1200u, result.OutputWidth);
        Assert.Equal(675u, result.OutputHeight);
    }

    [Fact]
    public async Task PreventUpscale_KeepsSmallerOriginalDimensions()
    {
        using var workspace = new TestWorkspace();
        ImageFileInfo source = workspace.CreateImage("small.jpg", 300, 200);
        var profile = new ExportProfile
        {
            Mode = TransformationMode.ResizeKeepAspect,
            Width = 1600,
            Height = 1600,
            PreventUpscale = true,
            Format = OutputImageFormat.Jpeg
        };

        ExportResult result = await ProcessAsync(workspace, source, profile, CropState.Full);

        Assert.True(result.Success, result.ErrorDetails);
        Assert.Equal(300u, result.OutputWidth);
        Assert.Equal(200u, result.OutputHeight);
    }

    [Theory]
    [InlineData("camera.heic")]
    [InlineData("camera.heif")]
    public async Task Loader_DecodesHeicAndHeif(string fileName)
    {
        using var workspace = new TestWorkspace();
        string inputPath = workspace.PathFor(fileName);
        string fixture = Path.Combine(AppContext.BaseDirectory, "Assets", "libheif-example.heic");
        File.Copy(fixture, inputPath);

        var paths = new AppPaths(workspace.PathFor("app-data"));
        ImageFileInfo loaded = await new ImageLoaderService(paths, new TestLogger()).LoadAsync(inputPath);

        Assert.True(loaded.Width > 0);
        Assert.True(loaded.Height > 0);
        Assert.True(File.Exists(loaded.ThumbnailPath));
        Assert.True(File.Exists(loaded.PreviewPath));
    }

    [Fact]
    public void RequiredReleaseCodecs_AreAvailable()
    {
        var service = new CapabilityService();

        service.EnsureReleaseCapabilities();

        IReadOnlyList<FormatCapability> capabilities = service.GetRequiredCapabilities();
        Assert.True(capabilities.Single(x => x.Name == "HEIC").CanRead);
        Assert.True(capabilities.Single(x => x.Name == "HEIF").CanRead);
    }

    private static async Task<ExportResult> ProcessAsync(
        TestWorkspace workspace, ImageFileInfo source, ExportProfile profile, CropState crop)
    {
        string extension = ExportPlanService.GetExtension(profile.Format);
        var job = new ExportJob(
            new ExportSource(source, crop, 0), profile,
            workspace.PathFor("result" + extension),
            workspace.PathFor(".temporary" + extension));
        return await new ImageProcessingService(new TestLogger()).ProcessAsync(job);
    }
}
