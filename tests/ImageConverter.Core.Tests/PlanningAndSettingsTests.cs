using ImageConverter.Core.Models;
using ImageConverter.Core.Services;

namespace ImageConverter.Core.Tests;

public sealed class PlanningAndSettingsTests
{
    [Fact]
    public void ExportProfile_RejectsQualityOutsideValidRange()
    {
        var profile = new ExportProfile { Quality = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(profile.Validate);
    }

    [Fact]
    public void ExportProfile_RejectsMissingResizeDimensions()
    {
        var profile = new ExportProfile
        {
            Mode = TransformationMode.CropCover,
            Width = 0,
            Height = 1050
        };

        Assert.Throws<ArgumentOutOfRangeException>(profile.Validate);
    }

    [Fact]
    public void PortfolioCardPreset_HasExpectedWebsiteSettings()
    {
        ExportProfile preset = ExportProfile.BuiltIn.Single(x => x.Name == "Portfolio Card");

        Assert.Equal(TransformationMode.CropCover, preset.Mode);
        Assert.Equal(600, preset.Width);
        Assert.Equal(1050, preset.Height);
        Assert.Equal(OutputImageFormat.WebP, preset.Format);
        Assert.Equal(95, preset.Quality);
        Assert.Equal(MetadataMode.RemoveExif, preset.Metadata);
    }

    [Theory]
    [InlineData(OutputImageFormat.WebP, ".webp")]
    [InlineData(OutputImageFormat.Jpeg, ".jpg")]
    [InlineData(OutputImageFormat.Png, ".png")]
    [InlineData(OutputImageFormat.Avif, ".avif")]
    public void GetExtension_ReturnsExpectedExtension(OutputImageFormat format, string expected)
    {
        Assert.Equal(expected, ExportPlanService.GetExtension(format));
    }

    [Fact]
    public void CreatePlan_UsesPrefixAndPaddedSequence()
    {
        using var workspace = new TestWorkspace();
        ImageFileInfo first = workspace.CreateImage("z.jpg");
        ImageFileInfo second = workspace.CreateImage("a.jpg");
        var sources = new[]
        {
            new ExportSource(first, CropState.Full, 0),
            new ExportSource(second, CropState.Full, 1)
        };
        var profile = new ExportProfile { Format = OutputImageFormat.WebP };

        IReadOnlyList<ExportJob> plan = new ExportPlanService().CreatePlan(
            sources, profile, workspace.PathFor("out"),
            new NamingOptions(NamingMode.PrefixSequence, "portfolio-", 1, 3));

        Assert.Equal("portfolio-001.webp", Path.GetFileName(plan[0].DestinationPath));
        Assert.Equal("portfolio-002.webp", Path.GetFileName(plan[1].DestinationPath));
        Assert.All(plan, job => Assert.Contains(".image-converter-export-", Path.GetFileName(job.TemporaryPath)));
    }

    [Fact]
    public void CreatePlan_DoesNotOverwriteExistingFile()
    {
        using var workspace = new TestWorkspace();
        ImageFileInfo source = workspace.CreateImage("photo.jpg");
        string output = workspace.PathFor("out");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "photo.webp"), "existing");

        IReadOnlyList<ExportJob> plan = new ExportPlanService().CreatePlan(
            [new ExportSource(source, CropState.Full, 0)],
            new ExportProfile { Format = OutputImageFormat.WebP }, output,
            new NamingOptions(NamingMode.KeepOriginal, "", 1, 1));

        Assert.Equal("photo_2.webp", Path.GetFileName(plan[0].DestinationPath));
    }

    [Fact]
    public void SizeEstimator_FormatOnlyKeepsDimensionsAndReturnsRange()
    {
        using var workspace = new TestWorkspace();
        ImageFileInfo source = workspace.CreateImage("photo.jpg", 1200, 800);
        var profile = new ExportProfile
        {
            Mode = TransformationMode.FormatOnly,
            Format = OutputImageFormat.WebP
        };

        ExportSizeEstimate estimate = ExportSizeEstimator.Estimate(source, profile, CropState.Full);

        Assert.Equal(1200, estimate.OutputWidth);
        Assert.Equal(800, estimate.OutputHeight);
        Assert.False(estimate.IsUpscaling);
        Assert.InRange(estimate.LowerBytes, 1, estimate.UpperBytes);
    }

    [Fact]
    public void SizeEstimator_ReportsUpscalingForSmallCrop()
    {
        using var workspace = new TestWorkspace();
        ImageFileInfo source = workspace.CreateImage("small.jpg", 300, 200);
        var profile = new ExportProfile
        {
            Mode = TransformationMode.CropCover,
            Width = 600,
            Height = 1050
        };

        ExportSizeEstimate estimate = ExportSizeEstimator.Estimate(
            source, profile, CropState.CreateCentered(1.5, 4d / 7d));

        Assert.True(estimate.IsUpscaling);
        Assert.Equal(600, estimate.OutputWidth);
        Assert.Equal(1050, estimate.OutputHeight);
        Assert.True(estimate.UpscaleFactor > 1);
    }

    [Fact]
    public async Task SettingsService_RoundTripsAndClampsUnsafeValues()
    {
        using var workspace = new TestWorkspace();
        var paths = new AppPaths(workspace.Root);
        var service = new SettingsService(paths, new TestLogger());
        var settings = new AppSettings
        {
            Theme = "Invalid",
            LastQuality = 1000,
            LastWidth = -20,
            LastHeight = 999999,
            NamingNumberLength = 99,
            ExportParallelism = 99
        };

        await service.SaveAsync(settings);
        AppSettings loaded = await service.LoadAsync();

        Assert.Equal("System", loaded.Theme);
        Assert.Equal(100, loaded.LastQuality);
        Assert.Equal(1, loaded.LastWidth);
        Assert.Equal(32768, loaded.LastHeight);
        Assert.Equal(8, loaded.NamingNumberLength);
        Assert.Equal(4, loaded.ExportParallelism);
    }
}
