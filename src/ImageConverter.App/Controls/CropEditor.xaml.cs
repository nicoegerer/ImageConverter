using System;
using ImageConverter.App.ViewModels;
using ImageConverter.Core.Models;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

namespace ImageConverter.App.Controls;

public sealed partial class CropEditor : UserControl
{
    private ImageAssetViewModel? _asset;

    private bool _dragging;

    private uint _pointerId;

    private Point _lastPointer;

    private Rect _imageRect;
    public int TargetWidth { get; set; } = 600;

    public int TargetHeight { get; set; } = 1050;

    public ImageAssetViewModel? Asset
    {
        get
        {
            return _asset;
        }
        set
        {
            _asset = value;
            PreviewImage.Source = value?.PreviewImage;
            EmptyState.Visibility = ((value != null) ? Visibility.Collapsed : Visibility.Visible);
            HintBar.Visibility = ((value == null) ? Visibility.Collapsed : Visibility.Visible);
            OverlayCanvas.Visibility = ((value == null) ? Visibility.Collapsed : Visibility.Visible);
            UpdateOverlay();
        }
    }

    public event EventHandler? CropChanged;

    public CropEditor()
    {
        InitializeComponent();
    }

    public void SetTarget(int width, int height)
    {
        TargetWidth = Math.Max(1, width);
        TargetHeight = Math.Max(1, height);
        CropLabelText.Text = $"{FormatRatio(TargetWidth, TargetHeight)} · {TargetWidth} × {TargetHeight}";
        UpdateOverlay();
    }

    public void MoveCrop(double normalizedX, double normalizedY)
    {
        if (_asset != null)
        {
            _asset.MoveCrop(normalizedX, normalizedY);
            UpdateOverlay();
            this.CropChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ZoomCrop(double scale)
    {
        if (_asset != null)
        {
            double imageAspect = (double)_asset.File.Width / (double)_asset.File.Height;
            double targetAspect = (double)TargetWidth / (double)TargetHeight;
            double relativeScale = _asset.Crop.GetRelativeScale(imageAspect, targetAspect);
            _asset.SetCropScale(targetAspect, Math.Clamp(relativeScale * scale, 0.05, 1.0));
            UpdateOverlay();
            this.CropChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetCropScale(double relativeScale)
    {
        if (_asset != null)
        {
            _asset.SetCropScale((double)TargetWidth / (double)TargetHeight, relativeScale);
            UpdateOverlay();
            this.CropChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ResetCrop()
    {
        if (_asset != null)
        {
            _asset.ResetCrop((double)TargetWidth / (double)TargetHeight);
            UpdateOverlay();
            this.CropChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Undo()
    {
        _asset?.Undo();
        UpdateOverlay();
        this.CropChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        _asset?.Redo();
        UpdateOverlay();
        this.CropChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        OverlayCanvas.Width = e.NewSize.Width;
        OverlayCanvas.Height = e.NewSize.Height;
        UpdateOverlay();
    }

    private void OnImageOpened(object sender, RoutedEventArgs e)
    {
        UpdateOverlay();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_asset != null && (e.Pointer.PointerDeviceType != PointerDeviceType.Touch || e.GetCurrentPoint(OverlayCanvas).Properties.IsLeftButtonPressed))
        {
            PointerPoint currentPoint = e.GetCurrentPoint(OverlayCanvas);
            if (currentPoint.Properties.IsLeftButtonPressed && GetCropRect().Contains(currentPoint.Position))
            {
                Focus(FocusState.Pointer);
                _dragging = true;
                _pointerId = e.Pointer.PointerId;
                _lastPointer = currentPoint.Position;
                _asset.BeginInteraction();
                OverlayCanvas.CapturePointer(e.Pointer);
                base.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
                e.Handled = true;
            }
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging && _asset != null && e.Pointer.PointerId == _pointerId && !(_imageRect.Width <= 0.0) && !(_imageRect.Height <= 0.0))
        {
            Point position = e.GetCurrentPoint(OverlayCanvas).Position;
            double deltaX = (position.X - _lastPointer.X) / _imageRect.Width;
            double deltaY = (position.Y - _lastPointer.Y) / _imageRect.Height;
            _lastPointer = position;
            _asset.UpdateInteraction(_asset.Crop.Move(deltaX, deltaY));
            UpdateOverlay();
            this.CropChanged?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndPointerInteraction(e.Pointer);
    }

    private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        EndPointerInteraction(e.Pointer);
    }

    private void EndPointerInteraction(Pointer pointer)
    {
        if (_dragging && pointer.PointerId == _pointerId)
        {
            _dragging = false;
            _asset?.CommitInteraction();
            OverlayCanvas.ReleasePointerCapture(pointer);
            base.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        }
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_asset != null)
        {
            int mouseWheelDelta = e.GetCurrentPoint(OverlayCanvas).Properties.MouseWheelDelta;
            ZoomCrop((mouseWheelDelta > 0) ? 0.94 : 1.06);
            e.Handled = true;
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        double num = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down) ? 0.001 : 0.005);
        switch (e.Key)
        {
            default:
                return;
            case VirtualKey.Left:
            case VirtualKey.A:
                MoveCrop(0.0 - num, 0.0);
                break;
            case VirtualKey.Right:
            case VirtualKey.D:
                MoveCrop(num, 0.0);
                break;
            case VirtualKey.Up:
            case VirtualKey.W:
                MoveCrop(0.0, 0.0 - num);
                break;
            case VirtualKey.Down:
            case VirtualKey.S:
                MoveCrop(0.0, num);
                break;
            case VirtualKey.R:
                ResetCrop();
                break;
            case VirtualKey.Add:
                ZoomCrop(0.94);
                break;
            case VirtualKey.Subtract:
                ZoomCrop(1.06);
                break;
        }
        e.Handled = true;
    }

    private void UpdateOverlay()
    {
        if (_asset != null && !(Viewport.ActualWidth <= 0.0) && !(Viewport.ActualHeight <= 0.0))
        {
            _imageRect = CalculateImageRect(Viewport.ActualWidth, Viewport.ActualHeight, _asset.File.Width, _asset.File.Height);
            Rect cropRect = GetCropRect();
            SetRect(TopMask, _imageRect.X, _imageRect.Y, _imageRect.Width, Math.Max(0.0, cropRect.Y - _imageRect.Y));
            SetRect(BottomMask, _imageRect.X, cropRect.Bottom, _imageRect.Width, Math.Max(0.0, _imageRect.Bottom - cropRect.Bottom));
            SetRect(LeftMask, _imageRect.X, cropRect.Y, Math.Max(0.0, cropRect.X - _imageRect.X), cropRect.Height);
            SetRect(RightMask, cropRect.Right, cropRect.Y, Math.Max(0.0, _imageRect.Right - cropRect.Right), cropRect.Height);
            SetRect(CropBorder, cropRect.X, cropRect.Y, cropRect.Width, cropRect.Height);
            SetVerticalThird(ThirdV1, cropRect, 1.0 / 3.0);
            SetVerticalThird(ThirdV2, cropRect, 2.0 / 3.0);
            SetHorizontalThird(ThirdH1, cropRect, 1.0 / 3.0);
            SetHorizontalThird(ThirdH2, cropRect, 2.0 / 3.0);
            CropLabelText.Text = $"{FormatRatio(TargetWidth, TargetHeight)} · {TargetWidth} × {TargetHeight}";
            CropLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(CropLabel, cropRect.X + 8.0);
            Canvas.SetTop(CropLabel, Math.Max(_imageRect.Y + 8.0, cropRect.Y + 8.0));
        }
    }

    private Rect GetCropRect()
    {
        if (_asset == null)
        {
            return Rect.Empty;
        }
        CropState crop = _asset.Crop;
        return new Rect(_imageRect.X + crop.X * _imageRect.Width, _imageRect.Y + crop.Y * _imageRect.Height, crop.Width * _imageRect.Width, crop.Height * _imageRect.Height);
    }

    private static Rect CalculateImageRect(double viewportWidth, double viewportHeight, double imageWidth, double imageHeight)
    {
        double num = Math.Min(viewportWidth / imageWidth, viewportHeight / imageHeight);
        double num2 = imageWidth * num;
        double num3 = imageHeight * num;
        return new Rect((viewportWidth - num2) / 2.0, (viewportHeight - num3) / 2.0, num2, num3);
    }

    private static void SetRect(FrameworkElement element, double x, double y, double width, double height)
    {
        element.Width = Math.Max(0.0, width);
        element.Height = Math.Max(0.0, height);
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
    }

    private static void SetVerticalThird(Line line, Rect rect, double fraction)
    {
        double x = (line.X2 = rect.X + rect.Width * fraction);
        line.X1 = x;
        line.Y1 = rect.Y;
        line.Y2 = rect.Bottom;
    }

    private static void SetHorizontalThird(Line line, Rect rect, double fraction)
    {
        double y = (line.Y2 = rect.Y + rect.Height * fraction);
        line.Y1 = y;
        line.X1 = rect.X;
        line.X2 = rect.Right;
    }

    private static string FormatRatio(int width, int height)
    {
        int num = Math.Abs(width);
        int num2 = Math.Abs(height);
        while (num2 != 0)
        {
            int num3 = num2;
            num2 = num % num2;
            num = num3;
        }
        int num4 = Math.Max(1, num);
        return $"{width / num4}:{height / num4}";
    }

}
