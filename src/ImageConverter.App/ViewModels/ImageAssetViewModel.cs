using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ImageConverter.Core.Models;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;

namespace ImageConverter.App.ViewModels;

public sealed class ImageAssetViewModel : ObservableObject
{
    private readonly Stack<CropState> _undo = new Stack<CropState>();

    private readonly Stack<CropState> _redo = new Stack<CropState>();

    private bool _isSelected = true;

    private CropState _crop;

    private CropState? _interactionStart;

    private string? _lastExportText;

    public ImageFileInfo File { get; }

    public string FileName => File.FileName;

    public string MetadataLine => $"{File.Width:N0} × {File.Height:N0} · {File.Format.ToUpperInvariant()}";

    public string FileSizeText => FormatBytes(File.FileSize);

    public BitmapImage ThumbnailImage { get; private set; } = new BitmapImage();

    public BitmapImage PreviewImage { get; private set; } = new BitmapImage();

    public bool IsSelected
    {
        get
        {
            return _isSelected;
        }
        set
        {
            SetProperty(ref _isSelected, value, "IsSelected");
        }
    }

    public CropState Crop
    {
        get
        {
            return _crop;
        }
        private set
        {
            if (SetProperty(ref _crop, value.Clamp(), "Crop"))
            {
                OnPropertyChanged("CanUndo");
                OnPropertyChanged("CanRedo");
            }
        }
    }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public string? LastExportText
    {
        get
        {
            return _lastExportText;
        }
        set
        {
            SetProperty(ref _lastExportText, value, "LastExportText");
        }
    }

    public ImageAssetViewModel(ImageFileInfo file, double targetAspect)
    {
        File = file;
        _crop = CropState.CreateCentered((double)file.Width / (double)file.Height, targetAspect);
    }

    public async Task LoadCachedImagesAsync()
    {
        ThumbnailImage = await LoadBitmapAsync(File.ThumbnailPath);
        PreviewImage = await LoadBitmapAsync(File.PreviewPath);
        OnPropertyChanged(nameof(ThumbnailImage));
        OnPropertyChanged(nameof(PreviewImage));
    }

    private static async Task<BitmapImage> LoadBitmapAsync(string path)
    {
        StorageFile file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenReadAsync();
        BitmapImage bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }

    public void ResetCrop(double targetAspect, bool addHistory = true)
    {
        SetCrop(CropState.CreateCentered((double)File.Width / (double)File.Height, targetAspect), addHistory);
    }

    public void BeginInteraction()
    {
        _interactionStart = Crop;
    }

    public void UpdateInteraction(CropState state)
    {
        Crop = state;
    }

    public void CommitInteraction()
    {
        if (_interactionStart is CropState interactionStart && interactionStart != Crop)
        {
            _undo.Push(interactionStart);
            _redo.Clear();
            OnPropertyChanged("CanUndo");
            OnPropertyChanged("CanRedo");
        }
        _interactionStart = null;
    }

    public void MoveCrop(double deltaX, double deltaY, bool addHistory = true)
    {
        SetCrop(Crop.Move(deltaX, deltaY), addHistory);
    }

    public void ZoomCrop(double scale, bool addHistory = true)
    {
        SetCrop(Crop.ZoomAroundCenter(scale), addHistory);
    }

    public void SetCropScale(double targetAspect, double relativeScale, bool addHistory = true)
    {
        SetCrop(Crop.WithRelativeScale((double)File.Width / (double)File.Height, targetAspect, relativeScale), addHistory);
    }

    public void AdaptCropAspect(double targetAspect, bool addHistory = true)
    {
        SetCrop(Crop.WithPixelAspect((double)File.Width / (double)File.Height, targetAspect), addHistory);
    }

    public void RestoreCrop(CropState crop)
    {
        _undo.Clear();
        _redo.Clear();
        Crop = crop;
        OnPropertyChanged("CanUndo");
        OnPropertyChanged("CanRedo");
    }

    public void Undo()
    {
        if (_undo.TryPop(out CropState? result) && result is not null)
        {
            _redo.Push(Crop);
            Crop = result;
        }
    }

    public void Redo()
    {
        if (_redo.TryPop(out CropState? result) && result is not null)
        {
            _undo.Push(Crop);
            Crop = result;
        }
    }

    private void SetCrop(CropState state, bool addHistory)
    {
        state = state.Clamp();
        if (!(state == Crop))
        {
            if (addHistory)
            {
                _undo.Push(Crop);
                _redo.Clear();
            }
            Crop = state;
        }
    }

    public static string FormatBytes(long bytes)
    {
        string[] array = new string[4] { "B", "KB", "MB", "GB" };
        double num = bytes;
        int num2 = 0;
        while (num >= 1024.0 && num2 < array.Length - 1)
        {
            num /= 1024.0;
            num2++;
        }
        return $"{num:0.#} {array[num2]}";
    }
}
