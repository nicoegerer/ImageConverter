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
    private enum InteractionMode
    {
        None,
        Move,
        Left,
        Top,
        Right,
        Bottom,
        TopLeft,
        TopRight,
        BottomRight,
        BottomLeft
    }

    private ImageAssetViewModel? _asset;

    private bool _dragging;

    private uint _pointerId;

    private Point _lastPointer;

    private CropState _interactionStartCrop = CropState.Full;

    private InteractionMode _interactionMode;

    private bool _cropEnabled = true;

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
            HintBar.Visibility = value is not null && _cropEnabled ? Visibility.Visible : Visibility.Collapsed;
            OverlayCanvas.Visibility = value is not null && _cropEnabled ? Visibility.Visible : Visibility.Collapsed;
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

    public void SetCropEnabled(bool enabled)
    {
        _cropEnabled = enabled;
        OverlayCanvas.Visibility = enabled && _asset is not null ? Visibility.Visible : Visibility.Collapsed;
        HintBar.Visibility = enabled && _asset is not null ? Visibility.Visible : Visibility.Collapsed;
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
        if (_cropEnabled && _asset != null && (e.Pointer.PointerDeviceType != PointerDeviceType.Touch || e.GetCurrentPoint(Viewport).Properties.IsLeftButtonPressed))
        {
            PointerPoint currentPoint = e.GetCurrentPoint(Viewport);
            InteractionMode mode = HitTestInteraction(currentPoint.Position, GetCropRect());
            if (currentPoint.Properties.IsLeftButtonPressed && mode != InteractionMode.None)
            {
                Viewport.Focus(FocusState.Pointer);
                _dragging = true;
                _pointerId = e.Pointer.PointerId;
                _lastPointer = currentPoint.Position;
                _interactionStartCrop = _asset.Crop;
                _interactionMode = mode;
                _asset.BeginInteraction();
                Viewport.CapturePointer(e.Pointer);
                base.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
                e.Handled = true;
            }
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging && _asset != null && e.Pointer.PointerId == _pointerId && !(_imageRect.Width <= 0.0) && !(_imageRect.Height <= 0.0))
        {
            Point position = e.GetCurrentPoint(Viewport).Position;
            if (_interactionMode == InteractionMode.Move)
            {
                double deltaX = (position.X - _lastPointer.X) / _imageRect.Width;
                double deltaY = (position.Y - _lastPointer.Y) / _imageRect.Height;
                _lastPointer = position;
                _asset.UpdateInteraction(_asset.Crop.Move(deltaX, deltaY));
            }
            else
            {
                _asset.UpdateInteraction(ResizeCrop(position));
            }
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
            _interactionMode = InteractionMode.None;
            _asset?.CommitInteraction();
            Viewport.ReleasePointerCapture(pointer);
            base.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        }
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_cropEnabled && _asset != null)
        {
            Viewport.Focus(FocusState.Pointer);
            int mouseWheelDelta = e.GetCurrentPoint(Viewport).Properties.MouseWheelDelta;
            ZoomCrop((mouseWheelDelta > 0) ? 0.94 : 1.06);
            e.Handled = true;
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_cropEnabled || _asset is null)
        {
            return;
        }
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
            SetHandle(HandleTopLeft, cropRect.Left, cropRect.Top);
            SetHandle(HandleTop, cropRect.X + cropRect.Width / 2.0, cropRect.Top);
            SetHandle(HandleTopRight, cropRect.Right, cropRect.Top);
            SetHandle(HandleRight, cropRect.Right, cropRect.Y + cropRect.Height / 2.0);
            SetHandle(HandleBottomRight, cropRect.Right, cropRect.Bottom);
            SetHandle(HandleBottom, cropRect.X + cropRect.Width / 2.0, cropRect.Bottom);
            SetHandle(HandleBottomLeft, cropRect.Left, cropRect.Bottom);
            SetHandle(HandleLeft, cropRect.Left, cropRect.Y + cropRect.Height / 2.0);
            CropLabelText.Text = $"{FormatRatio(TargetWidth, TargetHeight)} · {TargetWidth} × {TargetHeight}";
            CropLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(CropLabel, cropRect.X + 8.0);
            Canvas.SetTop(CropLabel, Math.Max(_imageRect.Y + 8.0, cropRect.Y + 8.0));
        }
    }

    private CropState ResizeCrop(Point pointer)
    {
        double x = Math.Clamp((pointer.X - _imageRect.X) / _imageRect.Width, 0.0, 1.0);
        double y = Math.Clamp((pointer.Y - _imageRect.Y) / _imageRect.Height, 0.0, 1.0);
        CropState start = _interactionStartCrop.Clamp();
        double imageAspect = (double)_asset!.File.Width / _asset.File.Height;
        double normalizedAspect = ((double)TargetWidth / TargetHeight) / imageAspect;
        double minWidth = Math.Max(0.005, 14.0 / _imageRect.Width);

        if (_interactionMode is InteractionMode.Left or InteractionMode.Right)
        {
            bool left = _interactionMode == InteractionMode.Left;
            double anchorX = left ? start.X + start.Width : start.X;
            double centerY = start.Y + start.Height / 2.0;
            double maxWidth = left ? anchorX : 1.0 - anchorX;
            maxWidth = Math.Min(maxWidth, 2.0 * Math.Min(centerY, 1.0 - centerY) * normalizedAspect);
            double width = Math.Clamp(Math.Abs(x - anchorX), Math.Min(minWidth, maxWidth), maxWidth);
            double height = width / normalizedAspect;
            return new CropState(left ? anchorX - width : anchorX, centerY - height / 2.0, width, height).Clamp();
        }

        if (_interactionMode is InteractionMode.Top or InteractionMode.Bottom)
        {
            bool top = _interactionMode == InteractionMode.Top;
            double anchorY = top ? start.Y + start.Height : start.Y;
            double centerX = start.X + start.Width / 2.0;
            double maxHeight = top ? anchorY : 1.0 - anchorY;
            maxHeight = Math.Min(maxHeight, 2.0 * Math.Min(centerX, 1.0 - centerX) / normalizedAspect);
            double minHeight = minWidth / normalizedAspect;
            double height = Math.Clamp(Math.Abs(y - anchorY), Math.Min(minHeight, maxHeight), maxHeight);
            double width = height * normalizedAspect;
            return new CropState(centerX - width / 2.0, top ? anchorY - height : anchorY, width, height).Clamp();
        }

        bool fromLeft = _interactionMode is InteractionMode.TopLeft or InteractionMode.BottomLeft;
        bool fromTop = _interactionMode is InteractionMode.TopLeft or InteractionMode.TopRight;
        double anchorCornerX = fromLeft ? start.X + start.Width : start.X;
        double anchorCornerY = fromTop ? start.Y + start.Height : start.Y;
        double maxCornerWidth = fromLeft ? anchorCornerX : 1.0 - anchorCornerX;
        double maxCornerHeight = fromTop ? anchorCornerY : 1.0 - anchorCornerY;
        double desiredWidth = Math.Max(Math.Abs(x - anchorCornerX), Math.Abs(y - anchorCornerY) * normalizedAspect);
        double availableWidth = Math.Min(maxCornerWidth, maxCornerHeight * normalizedAspect);
        double cornerWidth = Math.Clamp(desiredWidth, Math.Min(minWidth, availableWidth), availableWidth);
        double cornerHeight = cornerWidth / normalizedAspect;
        return new CropState(
            fromLeft ? anchorCornerX - cornerWidth : anchorCornerX,
            fromTop ? anchorCornerY - cornerHeight : anchorCornerY,
            cornerWidth,
            cornerHeight).Clamp();
    }

    private static InteractionMode HitTestInteraction(Point point, Rect rect)
    {
        const double hit = 16.0;
        bool left = Math.Abs(point.X - rect.Left) <= hit;
        bool right = Math.Abs(point.X - rect.Right) <= hit;
        bool top = Math.Abs(point.Y - rect.Top) <= hit;
        bool bottom = Math.Abs(point.Y - rect.Bottom) <= hit;
        bool withinX = point.X >= rect.Left - hit && point.X <= rect.Right + hit;
        bool withinY = point.Y >= rect.Top - hit && point.Y <= rect.Bottom + hit;
        if (left && top) return InteractionMode.TopLeft;
        if (right && top) return InteractionMode.TopRight;
        if (right && bottom) return InteractionMode.BottomRight;
        if (left && bottom) return InteractionMode.BottomLeft;
        if (left && withinY) return InteractionMode.Left;
        if (right && withinY) return InteractionMode.Right;
        if (top && withinX) return InteractionMode.Top;
        if (bottom && withinX) return InteractionMode.Bottom;
        return rect.Contains(point) ? InteractionMode.Move : InteractionMode.None;
    }

    private static void SetHandle(FrameworkElement handle, double centerX, double centerY)
    {
        Canvas.SetLeft(handle, centerX - handle.Width / 2.0);
        Canvas.SetTop(handle, centerY - handle.Height / 2.0);
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
