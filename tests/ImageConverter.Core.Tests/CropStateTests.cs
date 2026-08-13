using ImageConverter.Core.Models;

namespace ImageConverter.Core.Tests;

public sealed class CropStateTests
{
    [Fact]
    public void CreateCentered_LandscapeToPortrait_UsesFullHeightAndCentersHorizontally()
    {
        CropState crop = CropState.CreateCentered(16d / 9d, 4d / 7d);

        Assert.Equal(1d, crop.Height, 10);
        Assert.Equal((4d / 7d) / (16d / 9d), crop.Width, 10);
        Assert.Equal((1d - crop.Width) / 2d, crop.X, 10);
        Assert.Equal(0d, crop.Y, 10);
    }

    [Fact]
    public void Move_Down_IncreasesYWithoutInvertingDirection()
    {
        var crop = new CropState(0.2, 0.2, 0.5, 0.5);

        CropState moved = crop.Move(0, 0.1);

        Assert.Equal(0.3, moved.Y, 10);
    }

    [Fact]
    public void Move_ClampsCropInsideImage()
    {
        var crop = new CropState(0.2, 0.2, 0.5, 0.5);

        CropState moved = crop.Move(2, -2);

        Assert.Equal(0.5, moved.X, 10);
        Assert.Equal(0, moved.Y, 10);
    }

    [Fact]
    public void RelativeScale_IsAbsoluteAndReversible()
    {
        const double imageAspect = 16d / 9d;
        const double targetAspect = 4d / 7d;
        CropState crop = CropState.CreateCentered(imageAspect, targetAspect);

        CropState small = crop.WithRelativeScale(imageAspect, targetAspect, 0.25);
        CropState restored = small.WithRelativeScale(imageAspect, targetAspect, 1.0);

        Assert.Equal(crop.Width, restored.Width, 10);
        Assert.Equal(crop.Height, restored.Height, 10);
    }

    [Fact]
    public void AdaptAspect_PreservesRelativeZoom()
    {
        const double imageAspect = 1.5;
        CropState crop = CropState.CreateCentered(imageAspect, 4d / 7d)
            .WithRelativeScale(imageAspect, 4d / 7d, 0.6);

        CropState adapted = crop.WithPixelAspect(imageAspect, 1d / 2d);

        Assert.Equal(0.6, adapted.GetRelativeScale(imageAspect, 1d / 2d), 10);
        Assert.Equal(0.5, adapted.Width * imageAspect / adapted.Height, 10);
    }

    [Fact]
    public void Clamp_EnforcesMinimumAndValidBounds()
    {
        CropState crop = new CropState(-1, 2, 0, 3).Clamp();

        Assert.InRange(crop.Width, 0.005, 1);
        Assert.InRange(crop.Height, 0.005, 1);
        Assert.InRange(crop.X, 0, 1 - crop.Width);
        Assert.InRange(crop.Y, 0, 1 - crop.Height);
    }
}
