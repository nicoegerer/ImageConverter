using System;

namespace ImageConverter.Core.Models;

public sealed record CropState(double X, double Y, double Width, double Height)
{
    public static CropState Full => new CropState(0.0, 0.0, 1.0, 1.0);

    public static CropState CreateCentered(double imageAspect, double targetAspect)
    {
        if (imageAspect <= 0.0 || targetAspect <= 0.0)
        {
            return Full;
        }
        if (imageAspect > targetAspect)
        {
            double num = targetAspect / imageAspect;
            return new CropState((1.0 - num) / 2.0, 0.0, num, 1.0);
        }
        double num2 = imageAspect / targetAspect;
        return new CropState(0.0, (1.0 - num2) / 2.0, 1.0, num2);
    }

    public CropState Clamp()
    {
        double num = Math.Clamp(Width, 0.005, 1.0);
        double num2 = Math.Clamp(Height, 0.005, 1.0);
        double x = Math.Clamp(X, 0.0, 1.0 - num);
        double y = Math.Clamp(Y, 0.0, 1.0 - num2);
        return new CropState(x, y, num, num2);
    }

    public CropState Move(double deltaX, double deltaY)
    {
        return (this with
        {
            X = X + deltaX,
            Y = Y + deltaY
        }).Clamp();
    }

    public CropState ZoomAroundCenter(double scale)
    {
        scale = Math.Clamp(scale, 0.05, 1.25);
        CropState cropState = Clamp();
        double num = cropState.X + cropState.Width / 2.0;
        double num2 = cropState.Y + cropState.Height / 2.0;
        double min = Math.Max(0.005 / cropState.Width, 0.005 / cropState.Height);
        double max = Math.Min(1.0 / cropState.Width, 1.0 / cropState.Height);
        double num3 = Math.Clamp(scale, min, max);
        double num4 = cropState.Width * num3;
        double num5 = cropState.Height * num3;
        return new CropState(num - num4 / 2.0, num2 - num5 / 2.0, num4, num5).Clamp();
    }

    public CropState WithPixelAspect(double imageAspect, double targetAspect)
    {
        if (imageAspect <= 0.0 || targetAspect <= 0.0)
        {
            return this;
        }
        CropState cropState = Clamp();
        double targetAspect2 = cropState.Width * imageAspect / cropState.Height;
        double relativeScale = cropState.GetRelativeScale(imageAspect, targetAspect2);
        return cropState.WithRelativeScale(imageAspect, targetAspect, relativeScale);
    }

    public double GetRelativeScale(double imageAspect, double targetAspect)
    {
        if (imageAspect <= 0.0 || targetAspect <= 0.0)
        {
            return 1.0;
        }
        CropState cropState = Clamp();
        CropState cropState2 = CreateCentered(imageAspect, targetAspect);
        return Math.Clamp(Math.Min(cropState.Width / cropState2.Width, cropState.Height / cropState2.Height), 0.005, 1.0);
    }

    public CropState WithRelativeScale(double imageAspect, double targetAspect, double relativeScale)
    {
        if (imageAspect <= 0.0 || targetAspect <= 0.0)
        {
            return this;
        }
        CropState cropState = Clamp();
        CropState cropState2 = CreateCentered(imageAspect, targetAspect);
        double val = Math.Max(0.005 / cropState2.Width, 0.005 / cropState2.Height);
        relativeScale = Math.Clamp(relativeScale, Math.Min(val, 1.0), 1.0);
        double num = cropState.X + cropState.Width / 2.0;
        double num2 = cropState.Y + cropState.Height / 2.0;
        double num3 = cropState2.Width * relativeScale;
        double num4 = cropState2.Height * relativeScale;
        return new CropState(num - num3 / 2.0, num2 - num4 / 2.0, num3, num4).Clamp();
    }
}
