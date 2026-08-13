using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ImageConverter.App.Controls;
using ImageConverter.App.ViewModels;
using ImageConverter.Core.Models;
using ImageConverter.Core.Services;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;
using WinRT.Interop;

namespace ImageConverter.App;

public sealed partial class MainWindow : Window
{
    private readonly AppSettings _initialSettings;

    private readonly ImageLoaderService _loader;

    private readonly ExportPlanService _exportPlan = new ExportPlanService();

    private readonly ImageProcessingService _processor;

    private readonly ExportService _exporter;

    private readonly RenameService _renamer = new RenameService();

    private readonly GitHubUpdateService _updateService = new GitHubUpdateService();

    private readonly List<ExportResultViewModel> _lastResults = new List<ExportResultViewModel>();

    private CancellationTokenSource? _operationCancellation;

    private CancellationTokenSource? _estimateCancellation;

    private UpdateRelease? _availableUpdate;

    private DateTimeOffset? _lastUpdateCheckUtc;

    private bool _updatingControls = true;

    private double _lockedAspect = 0.5714285714285714;

    private string? _lastSourceFolder;

    private string? _destinationFolder;
    public ObservableCollection<ImageAssetViewModel> Images { get; } = new ObservableCollection<ImageAssetViewModel>();

    private ImageAssetViewModel? CurrentAsset => ImageList.SelectedItem as ImageAssetViewModel;

    public MainWindow(AppSettings settings)
    {
        _initialSettings = settings;
        _loader = new ImageLoaderService(App.Paths, App.Logger);
        _processor = new ImageProcessingService(App.Logger);
        _exporter = new ExportService(_processor);
        InitializeComponent();
        RootGrid.Loaded += OnRootGridLoaded;
        RootGrid.SizeChanged += OnRootGridSizeChanged;
        ConfigureWindow();
        InitializeOptions(settings);
        base.Closed += OnWindowClosed;
    }

    public async Task ImportExternalPathsAsync(IEnumerable<string> paths)
    {
        IEnumerable<string> inputPaths = paths.SelectMany((string path) => (!Directory.Exists(path)) ? new[] { path } : EnumerateImageFiles(path));
        await ImportPathsAsync(inputPaths);
    }

    private void ConfigureWindow()
    {
        base.ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        base.SystemBackdrop = new MicaBackdrop();
        AppWindow fromWindowId = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this)));
        DisplayArea displayArea = DisplayArea.GetFromWindowId(fromWindowId.Id, DisplayAreaFallback.Primary);
        RectInt32 workArea = displayArea.WorkArea;
        int windowWidth = Math.Min(1480, Math.Max(900, workArea.Width - 32));
        int windowHeight = Math.Min(920, Math.Max(600, workArea.Height - 32));
        fromWindowId.Resize(new SizeInt32(windowWidth, windowHeight));
        fromWindowId.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        if (fromWindowId.Presenter is OverlappedPresenter overlappedPresenter)
        {
            overlappedPresenter.PreferredMinimumWidth = Math.Min(1080, windowWidth);
            overlappedPresenter.PreferredMinimumHeight = Math.Min(640, windowHeight);
        }
    }

    private void InitializeOptions(AppSettings settings)
    {
        PresetCombo.ItemsSource = ExportProfile.BuiltIn;
        PresetCombo.DisplayMemberPath = "Name";
        ModeCombo.ItemsSource = new NamedOption<TransformationMode>[4]
        {
            new NamedOption<TransformationMode>("Originalgröße – nur Format und Dateigröße ändern", TransformationMode.OriginalSize),
            new NamedOption<TransformationMode>("Einpassen – komplettes Bild, maximale Größe", TransformationMode.ResizeKeepAspect),
            new NamedOption<TransformationMode>("Zuschneiden – exakte Zielgröße", TransformationMode.CropCover),
            new NamedOption<TransformationMode>("Strecken – exakte Größe (kann verzerren)", TransformationMode.ResizeStretch)
        };
        ModeCombo.DisplayMemberPath = "Label";
        FormatCombo.ItemsSource = new NamedOption<OutputImageFormat>[7]
        {
            new NamedOption<OutputImageFormat>("WebP", OutputImageFormat.WebP),
            new NamedOption<OutputImageFormat>("JPEG", OutputImageFormat.Jpeg),
            new NamedOption<OutputImageFormat>("PNG", OutputImageFormat.Png),
            new NamedOption<OutputImageFormat>("AVIF", OutputImageFormat.Avif),
            new NamedOption<OutputImageFormat>("TIFF", OutputImageFormat.Tiff),
            new NamedOption<OutputImageFormat>("BMP", OutputImageFormat.Bmp),
            new NamedOption<OutputImageFormat>("GIF", OutputImageFormat.Gif)
        };
        FormatCombo.DisplayMemberPath = "Label";
        MetadataCombo.ItemsSource = new NamedOption<MetadataMode>[4]
        {
            new NamedOption<MetadataMode>("EXIF behalten", MetadataMode.Keep),
            new NamedOption<MetadataMode>("GPS entfernen (empfohlen)", MetadataMode.RemoveGps),
            new NamedOption<MetadataMode>("EXIF entfernen", MetadataMode.RemoveExif),
            new NamedOption<MetadataMode>("Alles entfernen", MetadataMode.RemoveAll)
        };
        MetadataCombo.DisplayMemberPath = "Label";
        TransparencyCombo.ItemsSource = new NamedOption<TransparencyMode>[3]
        {
            new NamedOption<TransparencyMode>("Transparenz behalten", TransparencyMode.Preserve),
            new NamedOption<TransparencyMode>("Auf weißem Hintergrund", TransparencyMode.White),
            new NamedOption<TransparencyMode>("Auf schwarzem Hintergrund", TransparencyMode.Black)
        };
        TransparencyCombo.DisplayMemberPath = "Label";
        NamingModeCombo.ItemsSource = new NamedOption<NamingMode>[2]
        {
            new NamedOption<NamingMode>("Originalname beibehalten", NamingMode.KeepOriginal),
            new NamedOption<NamingMode>("Präfix + Nummer", NamingMode.PrefixSequence)
        };
        NamingModeCombo.DisplayMemberPath = "Label";
        RenameSortCombo.ItemsSource = new NamedOption<RenameSortMode>[5]
        {
            new NamedOption<RenameSortMode>("Aktueller Dateiname", RenameSortMode.CurrentFileName),
            new NamedOption<RenameSortMode>("Aufnahmezeit", RenameSortMode.CaptureTime),
            new NamedOption<RenameSortMode>("Änderungsdatum", RenameSortMode.ModifiedTime),
            new NamedOption<RenameSortMode>("Erstellungsdatum", RenameSortMode.CreatedTime),
            new NamedOption<RenameSortMode>("Manuelle Reihenfolge", RenameSortMode.ManualOrder)
        };
        RenameSortCombo.DisplayMemberPath = "Label";
        ThemeCombo.ItemsSource = new string[3] { "System", "Light", "Dark" };
        _updatingControls = true;
        TopNavigation.SelectedItem = TopNavigation.MenuItems[0];
        PresetCombo.SelectedIndex = -1;
        SelectOption(ModeCombo, NormalizeMode(settings.LastTransformationMode));
        SelectOption(FormatCombo, settings.LastOutputFormat);
        SelectOption(MetadataCombo, settings.LastMetadataMode);
        SelectOption(TransparencyCombo, settings.LastTransparencyMode);
        NamingModeCombo.SelectedIndex = 1;
        RenameSortCombo.SelectedIndex = 0;
        ThemeCombo.SelectedItem = settings.Theme;
        WidthBox.Value = settings.LastWidth;
        HeightBox.Value = settings.LastHeight;
        QualitySlider.Value = settings.LastQuality;
        QualityBox.Value = settings.LastQuality;
        NamingPrefixBox.Text = settings.NamingPrefix;
        RenamePrefixBox.Text = settings.NamingPrefix;
        NamingStartBox.Value = settings.NamingStartNumber;
        RenameStartBox.Value = settings.NamingStartNumber;
        NamingLengthBox.Value = settings.NamingNumberLength;
        RenameLengthBox.Value = settings.NamingNumberLength;
        ParallelismBox.Value = settings.ExportParallelism;
        RestoreSessionToggle.IsOn = settings.RestoreLastSession;
        AutoUpdateToggle.IsOn = settings.CheckForUpdates;
        _destinationFolder = settings.LastDestinationFolder;
        _lastSourceFolder = settings.LastSourceFolder;
        _lastUpdateCheckUtc = settings.LastUpdateCheckUtc;
        DestinationBox.Text = _destinationFolder ?? "Noch nicht gewählt";
        _lockedAspect = WidthBox.Value / HeightBox.Value;
        _updatingControls = false;
        ApplyTheme(settings.Theme);
        CurrentVersionText.Text = "v" + CurrentVersion().ToString(3);
        UpdateModeVisibility();
        UpdateFormatGuidance();
        UpdateSummary();
    }

    private static TransformationMode NormalizeMode(TransformationMode mode)
    {
        return mode switch
        {
            TransformationMode.FormatOnly => TransformationMode.OriginalSize,
            TransformationMode.FitContain => TransformationMode.ResizeKeepAspect,
            _ => mode
        };
    }

    private async void OnAddFilesClick(object sender, RoutedEventArgs e)
    {
        FileOpenPicker fileOpenPicker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            ViewMode = PickerViewMode.Thumbnail
        };
        foreach (string item in ImageLoaderService.SupportedExtensions.OrderBy((string x) => x))
        {
            fileOpenPicker.FileTypeFilter.Add(item);
        }
        InitializeWithWindow.Initialize(fileOpenPicker, WindowNative.GetWindowHandle(this));
        IReadOnlyList<StorageFile> readOnlyList = await fileOpenPicker.PickMultipleFilesAsync();
        if (readOnlyList.Count > 0)
        {
            await ImportPathsAsync(readOnlyList.Select((StorageFile x) => x.Path));
        }
    }

    private async void OnAddFolderClick(object sender, RoutedEventArgs e)
    {
        StorageFolder? storageFolder = await PickFolderAsync();
        if (storageFolder is not null)
        {
            _lastSourceFolder = storageFolder.Path;
            await ImportPathsAsync(EnumerateImageFiles(storageFolder.Path));
        }
    }

    private async Task<StorageFolder?> PickFolderAsync()
    {
        FolderPicker obj = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            FileTypeFilter = { "*" }
        };
        InitializeWithWindow.Initialize(obj, WindowNative.GetWindowHandle(this));
        return await obj.PickSingleFolderAsync();
    }

    private static IEnumerable<string> EnumerateImageFiles(string folder)
    {
        return (from path in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                where ImageLoaderService.SupportedExtensions.Contains(Path.GetExtension(path))
                select path).OrderBy<string, string>((string path) => path, StringComparer.CurrentCultureIgnoreCase);
    }

    private async Task ImportPathsAsync(IEnumerable<string> inputPaths)
    {
        string finalStatus = "Import abgeschlossen.";
        string[] paths = inputPaths
            .Where(_loader.IsSupportedPath)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(path => Images.All(image => !string.Equals(image.File.Path, path, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (paths.Length == 0)
        {
            SetStatus("Keine neuen unterstützten Bilddateien gefunden.");
            return;
        }

        _lastSourceFolder = Path.GetDirectoryName(paths[0]);
        _operationCancellation?.Cancel();
        _operationCancellation = new CancellationTokenSource();
        CancellationToken token = _operationCancellation.Token;
        SetBusy(busy: true, $"Lade 0 / {paths.Length} …");

        List<string> failures = [];
        ImageFileInfo?[] results = new ImageFileInfo[paths.Length];
        using var semaphore = new SemaphoreSlim(4);

        try
        {
            int completed = 0;
            await Task.WhenAll(paths.Select(async (path, index) =>
            {
                await semaphore.WaitAsync(token);
                try
                {
                    results[index] = await _loader.LoadAsync(path, token);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    lock (failures)
                    {
                        failures.Add($"{Path.GetFileName(path)}: {exception.GetBaseException().Message}");
                    }
                }
                finally
                {
                    semaphore.Release();
                    int count = Interlocked.Increment(ref completed);
                    DispatcherQueue.TryEnqueue(() => SetStatus($"Lade {count} / {paths.Length} …"));
                }
            }));

            ImageFileInfo[] loadedFiles = results.OfType<ImageFileInfo>().ToArray();
            foreach (ImageFileInfo item in loadedFiles)
            {
                var asset = new ImageAssetViewModel(item, CurrentTargetAspect());
                await asset.LoadCachedImagesAsync();
                asset.PropertyChanged += OnAssetPropertyChanged;
                Images.Add(asset);
            }

            if (ImageList.SelectedItem is null && Images.Count > 0)
            {
                ImageList.SelectedIndex = 0;
                ShowAsset(Images[0]);
            }

            UpdateSelectionCount();
            finalStatus = $"{loadedFiles.Length} Bild(er) geladen"
                + (failures.Count > 0 ? $" · {failures.Count} Fehler" : string.Empty);

            if (failures.Count > 0)
            {
                await ShowErrorListAsync("Einige Bilder konnten nicht geladen werden", failures);
            }
        }
        catch (OperationCanceledException)
        {
            finalStatus = "Import abgebrochen.";
        }
        finally
        {
            SetBusy(busy: false);
            await Task.Yield();
            SetStatus(finalStatus);
        }
    }

    private void OnRootGridSizeChanged(object sender, SizeChangedEventArgs args)
    {
        ApplyResponsiveLayout(args.NewSize.Width);
    }

    private async void OnRootGridLoaded(object sender, RoutedEventArgs args)
    {
        ApplyResponsiveLayout(RootGrid.ActualWidth);
        RootGrid.Loaded -= OnRootGridLoaded;
        if (RestoreSessionToggle.IsOn && Images.Count == 0)
        {
            await RestoreLastSessionAsync();
        }
        if (AutoUpdateToggle.IsOn)
        {
            await CheckForUpdatesAsync(showNoUpdateMessage: false);
        }
    }

    private async Task RestoreLastSessionAsync()
    {
        SessionImageState[] session = _initialSettings.SessionImages
            .Where(item => File.Exists(item.Path))
            .ToArray();
        if (session.Length == 0)
        {
            return;
        }

        await ImportPathsAsync(session.Select(item => item.Path));
        Dictionary<string, SessionImageState> states = session.ToDictionary(item => Path.GetFullPath(item.Path), StringComparer.OrdinalIgnoreCase);
        foreach (ImageAssetViewModel image in Images)
        {
            if (states.TryGetValue(image.File.Path, out SessionImageState? state))
            {
                image.RestoreCrop(state.Crop);
                image.IsSelected = state.IsSelected;
                image.LastExportText = state.LastExportText;
            }
        }

        ImageAssetViewModel? selected = Images.FirstOrDefault(image => string.Equals(image.File.Path, _initialSettings.SelectedImagePath, StringComparison.OrdinalIgnoreCase))
            ?? Images.FirstOrDefault();
        if (selected is not null)
        {
            ImageList.SelectedItem = selected;
            ShowAsset(selected);
        }
        UpdateSelectionCount();
        SetStatus($"Letzte Sitzung wiederhergestellt · {Images.Count} Bild(er)");
    }

    private void ApplyResponsiveLayout(double windowWidth)
    {
        if (windowWidth < 1300)
        {
            ImagesColumn.Width = new GridLength(220);
            PreviewColumn.Width = new GridLength(300);
            OptionsColumn.Width = new GridLength(1, GridUnitType.Star);
            HeaderAddText.Text = "Bilder";
            HeaderFolderButton.Visibility = Visibility.Collapsed;
            HeaderExportButton.Visibility = Visibility.Collapsed;
            return;
        }

        ImagesColumn.Width = new GridLength(325);
        PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
        OptionsColumn.Width = new GridLength(375);
        HeaderAddText.Text = "Bilder hinzufügen";
        HeaderFolderButton.Visibility = Visibility.Visible;
        HeaderExportButton.Visibility = Visibility.Visible;
    }

    private void OnAssetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "IsSelected")
        {
            UpdateSelectionCount();
        }
    }

    private void OnImageItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ImageAssetViewModel imageAssetViewModel)
        {
            ImageList.SelectedItem = imageAssetViewModel;
            ShowAsset(imageAssetViewModel);
        }
    }

    private void OnImageContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue)
        {
            args.ItemContainer.IsTabStop = true;
        }
    }

    private void ShowAsset(ImageAssetViewModel asset)
    {
        CropEditor.Asset = asset;
        PreviewTitle.Text = asset.FileName;
        ToolTipService.SetToolTip(PreviewTitle, asset.FileName);
        PreviewSubtitle.Text = asset.MetadataLine + " · Original " + asset.FileSizeText;
        OriginalSizeText.Text = asset.FileSizeText;
        OriginalDimensionsText.Text = $"{asset.File.Width:N0} × {asset.File.Height:N0} Pixel";
        OriginalAspectText.Text = FormatRatio((int)asset.File.Width, (int)asset.File.Height);
        SyncZoomSliderToCurrentCrop();
        UpdateSummary();
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (!(args.SelectedItemContainer?.Tag is string text))
        {
            return;
        }
        bool isUtilityPage = text is "rename" or "settings";
        ExportScroll.Visibility = isUtilityPage ? Visibility.Collapsed : Visibility.Visible;
        RenameScroll.Visibility = ((!(text == "rename")) ? Visibility.Collapsed : Visibility.Visible);
        SettingsScroll.Visibility = ((!(text == "settings")) ? Visibility.Collapsed : Visibility.Visible);
    }

    private static void SelectOption<T>(ComboBox comboBox, T value) where T : struct, Enum
    {
        comboBox.SelectedItem = comboBox.Items.OfType<NamedOption<T>>().First((NamedOption<T> x) => EqualityComparer<T>.Default.Equals(x.Value, value));
    }

    private void OnPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingControls && PresetCombo.SelectedItem is ExportProfile profile)
        {
            ApplyPreset(profile, preserveStoredFormatAndQuality: false);
        }
    }

    private void ApplyPreset(ExportProfile profile, bool preserveStoredFormatAndQuality)
    {
        _updatingControls = true;
        SelectOption(ModeCombo, profile.Mode);
        WidthBox.Value = ((profile.Width > 0) ? profile.Width : Math.Max(1, _initialSettings.LastWidth));
        HeightBox.Value = ((profile.Height > 0) ? profile.Height : Math.Max(1, _initialSettings.LastHeight));
        if (!preserveStoredFormatAndQuality)
        {
            SelectOption(FormatCombo, profile.Format);
            QualitySlider.Value = profile.Quality;
            QualityBox.Value = profile.Quality;
        }
        SelectOption(MetadataCombo, profile.Metadata);
        PreventUpscaleCheck.IsChecked = profile.PreventUpscale;
        _lockedAspect = WidthBox.Value / HeightBox.Value;
        _updatingControls = false;
        AdaptAllCropsToTarget();
        UpdateModeVisibility();
        UpdateSummary();
    }

    private void OnModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingControls)
        {
            PresetCombo.SelectedIndex = -1;
            UpdateModeVisibility();
            AdaptAllCropsToTarget();
            UpdateSummary();
        }
    }

    private void UpdateModeVisibility()
    {
        TransformationMode transformationMode = CurrentMode();
        bool hasTargetSize = transformationMode is not TransformationMode.FormatOnly and not TransformationMode.OriginalSize;
        bool hasExactDimensions = transformationMode is TransformationMode.CropCover or TransformationMode.ResizeStretch;
        SizeFields.Visibility = hasTargetSize ? Visibility.Visible : Visibility.Collapsed;
        AspectLockCheck.IsEnabled = hasTargetSize && !hasExactDimensions;
        AspectLockCheck.Visibility = transformationMode == TransformationMode.ResizeKeepAspect ? Visibility.Visible : Visibility.Collapsed;
        CropSizeHint.Visibility = ((transformationMode != TransformationMode.CropCover) ? Visibility.Collapsed : Visibility.Visible);
        PreventUpscaleCheck.Visibility = hasTargetSize ? Visibility.Visible : Visibility.Collapsed;
        bool isCrop = transformationMode == TransformationMode.CropCover;
        CropEditor.SetCropEnabled(isCrop);
        CropToolbar.Visibility = isCrop ? Visibility.Visible : Visibility.Collapsed;
        ZoomControls.Visibility = isCrop ? Visibility.Visible : Visibility.Collapsed;
        ModeHelpText.Text = transformationMode switch
        {
            TransformationMode.OriginalSize or TransformationMode.FormatOnly => "Die Pixel bleiben unverändert. Nur Format, Qualität, Transparenz und Metadaten beeinflussen die Datei.",
            TransformationMode.ResizeKeepAspect or TransformationMode.FitContain => "Das komplette Bild bleibt sichtbar und wird proportional in die maximale Breite und Höhe eingepasst.",
            TransformationMode.CropCover => "Du legst exakte Zielpixel fest. Verschiebe oder vergrößere den Rahmen direkt in der Vorschau.",
            TransformationMode.ResizeStretch => "Breite und Höhe werden exakt erzwungen. Das Bild kann dabei sichtbar verzerrt werden.",
            _ => string.Empty
        };
    }

    private void OnSizeChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_updatingControls || double.IsNaN(args.NewValue) || args.NewValue <= 0.0)
        {
            return;
        }
        TransformationMode mode = CurrentMode();
        bool shouldApplyLock = AspectLockCheck.IsChecked == true
            && mode is not TransformationMode.CropCover
            && mode is not TransformationMode.ResizeStretch;

        if (shouldApplyLock)
        {
            _updatingControls = true;
            try
            {
                if (ReferenceEquals(sender, WidthBox))
                {
                    HeightBox.Value = Math.Max(1.0, Math.Round(WidthBox.Value / _lockedAspect));
                }
                else
                {
                    WidthBox.Value = Math.Max(1.0, Math.Round(HeightBox.Value * _lockedAspect));
                }
            }
            finally
            {
                _updatingControls = false;
            }
        }
        else
        {
            _lockedAspect = WidthBox.Value / HeightBox.Value;
        }

        PresetCombo.SelectedIndex = -1;
        AdaptAllCropsToTarget();
        UpdateSummary();
    }

    private void OnAspectLockChanged(object sender, RoutedEventArgs e)
    {
        if (AspectLockCheck.IsChecked == true && WidthBox.Value > 0.0 && HeightBox.Value > 0.0)
        {
            _lockedAspect = WidthBox.Value / HeightBox.Value;
        }
        if (!_updatingControls)
        {
            PresetCombo.SelectedIndex = -1;
        }
    }

    private void AdaptAllCropsToTarget()
    {
        if (CurrentMode() != TransformationMode.CropCover)
        {
            return;
        }
        double targetAspect = CurrentTargetAspect();
        foreach (ImageAssetViewModel image in Images)
        {
            image.AdaptCropAspect(targetAspect);
        }
        CropEditor.SetTarget(CurrentWidth(), CurrentHeight());
        SyncZoomSliderToCurrentCrop();
    }

    private void SyncZoomSliderToCurrentCrop()
    {
        if (CurrentAsset != null && CurrentMode() == TransformationMode.CropCover)
        {
            double imageAspect = (double)CurrentAsset.File.Width / (double)CurrentAsset.File.Height;
            double relativeScale = CurrentAsset.Crop.GetRelativeScale(imageAspect, CurrentTargetAspect());
            double num = 100.0 / relativeScale;
            _updatingControls = true;
            ZoomSlider.Value = Math.Clamp(num, ZoomSlider.Minimum, ZoomSlider.Maximum);
            _updatingControls = false;
            ZoomValueText.Text = ((num > ZoomSlider.Maximum) ? $"> {ZoomSlider.Maximum:0} %" : $"{num:0} %");
        }
    }

    private void OnQualitySliderChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_updatingControls)
        {
            _updatingControls = true;
            QualityBox.Value = Math.Round(e.NewValue);
            _updatingControls = false;
            PresetCombo.SelectedIndex = -1;
            UpdateSummary();
        }
    }

    private void OnQualityBoxChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_updatingControls && !double.IsNaN(args.NewValue))
        {
            _updatingControls = true;
            QualitySlider.Value = Math.Clamp(args.NewValue, 1.0, 100.0);
            _updatingControls = false;
            PresetCombo.SelectedIndex = -1;
            UpdateSummary();
        }
    }

    private void OnQualityPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && double.TryParse(tag, out var result))
        {
            QualitySlider.Value = result;
        }
    }

    private void OnExportSettingChanged(object sender, object e)
    {
        if (!_updatingControls && (ReferenceEquals(sender, FormatCombo)
            || ReferenceEquals(sender, MetadataCombo)
            || ReferenceEquals(sender, TransparencyCombo)
            || ReferenceEquals(sender, PreventUpscaleCheck)))
        {
            PresetCombo.SelectedIndex = -1;
        }
        UpdateFormatGuidance();
        UpdateSummary();
    }

    private void UpdateFormatGuidance()
    {
        if (FormatCombo.SelectedItem is not NamedOption<OutputImageFormat> selected)
        {
            return;
        }

        FormatHelpText.Text = selected.Value switch
        {
            OutputImageFormat.WebP => "Empfohlen für kleine Web-Dateien. Unterstützt Transparenz und liefert meist deutlich kleinere Fotos als JPEG.",
            OutputImageFormat.Jpeg => "Sehr kompatibel für Fotos, aber ohne Transparenz. Transparente Bereiche werden auf den gewählten Hintergrund gesetzt.",
            OutputImageFormat.Png => "Verlustfrei und transparent, bei Fotos jedoch oft deutlich größer als WebP oder AVIF.",
            OutputImageFormat.Avif => "Sehr kleine Dateien bei guter Qualität und Transparenz; Export und Öffnen können länger dauern.",
            OutputImageFormat.Tiff => "Für Archivierung und Weiterbearbeitung. Hohe Qualität, gewöhnlich große Dateien.",
            OutputImageFormat.Bmp => "Unkomprimiert und sehr groß. Nur für spezielle ältere Programme sinnvoll.",
            OutputImageFormat.Gif => "Für einfache Grafiken mit wenigen Farben; Fotos verlieren sichtbar an Qualität.",
            _ => string.Empty
        };
        bool supportsAlpha = selected.Value is OutputImageFormat.WebP or OutputImageFormat.Png
            or OutputImageFormat.Avif or OutputImageFormat.Tiff or OutputImageFormat.Gif;
        bool usesQuality = selected.Value is OutputImageFormat.WebP or OutputImageFormat.Jpeg or OutputImageFormat.Avif;
        QualityLabelText.Text = usesQuality ? "Qualität" : "Qualität (für dieses Format nicht nötig)";
        QualityBox.IsEnabled = usesQuality;
        QualitySlider.IsEnabled = usesQuality;
        QualityPresetPanel.IsHitTestVisible = usesQuality;
        QualityPresetPanel.Opacity = usesQuality ? 1.0 : 0.45;
        TransparencyHelpText.Text = supportsAlpha
            ? "Bei ‚Transparenz behalten‘ bleiben durchsichtige Bereiche erhalten. Weiß oder Schwarz entfernt die Transparenz bewusst."
            : "Dieses Format unterstützt hier keine verlässliche Transparenz; transparente Bereiche werden auf Weiß oder Schwarz gesetzt.";
    }

    private void OnNamingModeChanged(object sender, SelectionChangedEventArgs e)
    {
        NamingFields.Visibility = ((CurrentNamingMode() != NamingMode.PrefixSequence) ? Visibility.Collapsed : Visibility.Visible);
        UpdateSummary();
    }

    private void OnZoomSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_updatingControls && CurrentAsset != null)
        {
            double num = Math.Max(100.0, e.NewValue);
            CropEditor.SetCropScale(100.0 / num);
            ZoomValueText.Text = $"{num:0} %";
        }
    }

    private void OnCropChanged(object? sender, EventArgs e)
    {
        SyncZoomSliderToCurrentCrop();
        UpdateSummary();
    }

    private void OnUndoClick(object sender, RoutedEventArgs e)
    {
        CropEditor.Undo();
    }

    private void OnRedoClick(object sender, RoutedEventArgs e)
    {
        CropEditor.Redo();
    }

    private void OnResetCropClick(object sender, RoutedEventArgs e)
    {
        CropEditor.ResetCrop();
    }

    private async void OnChooseDestinationClick(object sender, RoutedEventArgs e)
    {
        StorageFolder? storageFolder = await PickFolderAsync();
        if (storageFolder is not null)
        {
            _destinationFolder = storageFolder.Path;
            DestinationBox.Text = storageFolder.Path;
        }
    }

    private async void OnExportSelectedClick(object sender, RoutedEventArgs e)
    {
        await StartExportAsync(Images.Where((ImageAssetViewModel x) => x.IsSelected).ToArray());
    }

    private async void OnExportCurrentClick(object sender, RoutedEventArgs e)
    {
        if (CurrentAsset is null)
        {
            await ShowMessageAsync("Kein Bild geöffnet", "Wähle links zuerst das Bild aus, das du exportieren möchtest.");
            return;
        }
        await StartExportAsync([CurrentAsset]);
    }

    private async void OnExportAllClick(object sender, RoutedEventArgs e)
    {
        await StartExportAsync(Images.ToArray());
    }

    private async Task StartExportAsync(IReadOnlyList<ImageAssetViewModel> assets)
    {
        if (assets.Count == 0)
        {
            await ShowMessageAsync("Keine Bilder ausgewählt", "Wähle links mindestens ein Bild für den Export aus.");
            return;
        }
        if (string.IsNullOrWhiteSpace(_destinationFolder))
        {
            StorageFolder? storageFolder = await PickFolderAsync();
            if (storageFolder is null)
            {
                return;
            }
            _destinationFolder = storageFolder.Path;
            DestinationBox.Text = storageFolder.Path;
        }
        ExportProfile exportProfile = BuildProfile();
        ExportSource[] sources = assets.Select((ImageAssetViewModel asset, int index) => new ExportSource(asset.File, asset.Crop, index)).ToArray();
        NamingOptions naming = new NamingOptions(CurrentNamingMode(), string.IsNullOrWhiteSpace(NamingPrefixBox.Text) ? "Bild" : NamingPrefixBox.Text.Trim(), (int)NamingStartBox.Value, (int)NamingLengthBox.Value);
        IReadOnlyList<ExportJob> plan;
        try
        {
            plan = _exportPlan.CreatePlan(sources, exportProfile, _destinationFolder, naming);
        }
        catch (Exception ex)
        {
            App.Logger.Error("Export.Plan", ex, _destinationFolder);
            await ShowMessageAsync("Export kann nicht vorbereitet werden", ex.Message);
            return;
        }
        IEnumerable<string> values = from x in plan.Take(12)
                                     select x.Source.File.FileName + "  →  " + Path.GetFileName(x.DestinationPath);
        string text = ((plan.Count > 12) ? $"\n… und {plan.Count - 12} weitere" : string.Empty);
        if (!(await ShowConfirmationAsync("Export bestätigen", $"{exportProfile.Name}\n{DescribeProfile(exportProfile)}\nZiel: {_destinationFolder}\n\n" + string.Join("\n", values) + text, "Exportieren")))
        {
            return;
        }
        _operationCancellation?.Cancel();
        _operationCancellation = new CancellationTokenSource();
        CancellationToken token = _operationCancellation.Token;
        _lastResults.Clear();
        ResultsButton.Visibility = Visibility.Collapsed;
        ExportProgressBar.Visibility = Visibility.Visible;
        ExportProgressBar.Value = 0.0;
        CancelExportButton.Visibility = Visibility.Visible;
        SetBusy(busy: true, $"Exportiere 0 / {plan.Count} …");
        try
        {
            Progress<ExportProgress> progress = new Progress<ExportProgress>(item =>
            {
                ExportProgressBar.Value = item.Percentage;
                SetStatus($"Exportiere {item.Completed} / {item.Total} · {item.CurrentFile}");
            });
            IReadOnlyList<ExportResult> readOnlyList = await _exporter.ExportAsync(plan, (int)ParallelismBox.Value, progress, token);
            _lastResults.AddRange(readOnlyList.Select(ExportResultViewModel.From));
            foreach (ExportResult result in readOnlyList.Where((ExportResult x) => x.Success))
            {
                ImageAssetViewModel? imageAssetViewModel = assets.FirstOrDefault(x => x.File.Path == result.Job.Source.File.Path);
                if (imageAssetViewModel != null)
                {
                    imageAssetViewModel.LastExportText = $"✓ {result.OutputWidth} × {result.OutputHeight} · {ImageAssetViewModel.FormatBytes(result.OutputBytes)}";
                }
            }
            int num = readOnlyList.Count((ExportResult x) => x.Success);
            int num2 = readOnlyList.Count - num;
            SetStatus($"Export abgeschlossen · {num} erfolgreich" + ((num2 > 0) ? $" · {num2} Fehler" : ""));
            ResultsButtonText.Text = $"Ergebnisse ({num}/{readOnlyList.Count})";
            ResultsButton.Visibility = Visibility.Visible;
            await ShowExportSummaryAsync(readOnlyList);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Export abgebrochen. Bereits fertige Dateien bleiben erhalten.");
        }
        finally
        {
            SetBusy(busy: false);
            CancelExportButton.Visibility = Visibility.Collapsed;
            ExportProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private void OnCancelExportClick(object sender, RoutedEventArgs e)
    {
        _operationCancellation?.Cancel();
    }

    private async void OnPreviewRenameClick(object sender, RoutedEventArgs e)
    {
        ImageAssetViewModel[] array = Images.Where((ImageAssetViewModel x) => x.IsSelected).ToArray();
        if (array.Length == 0)
        {
            await ShowMessageAsync("Keine Bilder ausgewählt", "Wähle links die Dateien aus, die umbenannt werden sollen.");
            return;
        }
        try
        {
            RenameOptions options = new RenameOptions(string.IsNullOrWhiteSpace(RenamePrefixBox.Text) ? "Bild" : RenamePrefixBox.Text.Trim(), (int)RenameStartBox.Value, (int)RenameLengthBox.Value, ((NamedOption<RenameSortMode>)RenameSortCombo.SelectedItem).Value);
            IReadOnlyList<RenamePlanItem> plan = _renamer.CreatePlan(array.Select((ImageAssetViewModel x) => x.File).ToArray(), options);
            IEnumerable<string> values = plan.Select((RenamePlanItem x) => Path.GetFileName(x.SourcePath) + "  →  " + Path.GetFileName(x.DestinationPath));
            if (!(await ShowConfirmationAsync("Umbenennen bestätigen", string.Join("\n", values), "Umbenennen")))
            {
                return;
            }
            _renamer.Execute(plan);
            await ReloadAfterRenameAsync(plan);
            SetStatus($"{plan.Count} Datei(en) sicher umbenannt.");
        }
        catch (Exception ex)
        {
            App.Logger.Error("Rename", ex);
            await ShowMessageAsync("Umbenennen nicht möglich", ex.Message);
        }
    }

    private async void OnPreviewSingleRenameClick(object sender, RoutedEventArgs e)
    {
        ImageAssetViewModel[] assets = Images.Where((ImageAssetViewModel x) => x.IsSelected).ToArray();
        if (assets.Length != 1)
        {
            await ShowMessageAsync("Genau ein Bild auswählen", "Wähle links genau eine Datei aus, um ihr einen individuellen Namen zu geben.");
            return;
        }
        try
        {
            IReadOnlyList<RenamePlanItem> plan = _renamer.CreateSinglePlan(assets[0].File, SingleRenameBox.Text);
            RenamePlanItem item = plan[0];
            if (!(await ShowConfirmationAsync("Einzelnes Bild umbenennen", Path.GetFileName(item.SourcePath) + "  →  " + Path.GetFileName(item.DestinationPath) + "\n\nDas Bildformat bleibt unverändert.", "Umbenennen")))
            {
                return;
            }
            _renamer.Execute(plan);
            await ReloadAfterRenameAsync(plan);
            SingleRenameBox.Text = string.Empty;
            SetStatus("Datei sicher in " + Path.GetFileName(item.DestinationPath) + " umbenannt.");
        }
        catch (Exception ex)
        {
            App.Logger.Error("Rename.Single", ex, assets[0].File.Path);
            await ShowMessageAsync("Umbenennen nicht möglich", ex.Message);
        }
    }

    private async Task ReloadAfterRenameAsync(IReadOnlyList<RenamePlanItem> plan)
    {
        Dictionary<string, string> replacements = plan.ToDictionary<RenamePlanItem, string, string>((RenamePlanItem x) => x.SourcePath, (RenamePlanItem x) => x.DestinationPath, StringComparer.OrdinalIgnoreCase);
        var state = Images.Select((ImageAssetViewModel asset) => new
        {
            Path = replacements.GetValueOrDefault(asset.File.Path, asset.File.Path),
            Crop = asset.Crop,
            IsSelected = asset.IsSelected,
            LastExportText = asset.LastExportText
        }).ToArray();
        foreach (ImageAssetViewModel image in Images)
        {
            image.PropertyChanged -= OnAssetPropertyChanged;
        }
        Images.Clear();
        CropEditor.Asset = null;
        await ImportPathsAsync(state.Select(x => x.Path));
        var dictionary = state.ToDictionary(x => x.Path, StringComparer.OrdinalIgnoreCase);
        foreach (ImageAssetViewModel image2 in Images)
        {
            if (dictionary.TryGetValue(image2.File.Path, out var value))
            {
                image2.RestoreCrop(value.Crop);
                image2.IsSelected = value.IsSelected;
                image2.LastExportText = value.LastExportText;
            }
        }
        UpdateSelectionCount();
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeCombo.SelectedItem is string theme)
        {
            ApplyTheme(theme);
        }
    }

    private void ApplyTheme(string theme)
    {
        Grid rootGrid = RootGrid;
        ElementTheme requestedTheme = ((theme == "Light") ? ElementTheme.Light : ((theme == "Dark") ? ElementTheme.Dark : ElementTheme.Default));
        rootGrid.RequestedTheme = requestedTheme;
    }

    private async void OnCheckUpdateClick(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(showNoUpdateMessage: true);
    }

    private async Task CheckForUpdatesAsync(bool showNoUpdateMessage)
    {
        try
        {
            SetBusy(busy: true, "Suche nach Updates …");
            _lastUpdateCheckUtc = DateTimeOffset.UtcNow;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            _availableUpdate = await _updateService.CheckForUpdateAsync(CurrentVersion(), timeout.Token);
            if (_availableUpdate is null)
            {
                UpdateInfoBar.IsOpen = false;
                SetStatus("Image Converter ist aktuell.");
                if (showNoUpdateMessage)
                {
                    await ShowMessageAsync("Kein Update verfügbar", $"Du verwendest bereits Version {CurrentVersion().ToString(3)}.");
                }
                return;
            }

            UpdateInfoText.Text = $"Version {_availableUpdate.Version.ToString(3)} kann direkt heruntergeladen und installiert werden.";
            UpdateInfoBar.IsOpen = true;
            SetStatus($"Update {_availableUpdate.Version.ToString(3)} verfügbar.");
        }
        catch (Exception exception)
        {
            App.Logger.Error("Update.Check", exception);
            SetStatus("Update-Prüfung war nicht möglich.");
            if (showNoUpdateMessage)
            {
                await ShowMessageAsync("Update-Prüfung fehlgeschlagen", "GitHub konnte gerade nicht erreicht werden. Prüfe deine Internetverbindung und versuche es später erneut.");
            }
        }
        finally
        {
            SetBusy(busy: false);
        }
    }

    private async void OnInstallUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_availableUpdate is null)
        {
            await CheckForUpdatesAsync(showNoUpdateMessage: true);
            if (_availableUpdate is null)
            {
                return;
            }
        }

        if (!await ShowConfirmationAsync(
            "Update installieren",
            $"Version {_availableUpdate.Version.ToString(3)} wird von GitHub geladen, per SHA-256 geprüft und anschließend installiert. Image Converter wird dafür geschlossen.",
            "Herunterladen und installieren"))
        {
            return;
        }

        InstallUpdateButton.IsEnabled = false;
        ExportProgressBar.Visibility = Visibility.Visible;
        ExportProgressBar.Value = 0;
        SetBusy(busy: true, "Update wird heruntergeladen …");
        try
        {
            var progress = new Progress<double>(value =>
            {
                ExportProgressBar.Value = value * 100.0;
                SetStatus($"Update wird heruntergeladen · {value:P0}");
            });
            string installer = await _updateService.DownloadInstallerAsync(_availableUpdate, progress);
            var startInfo = new ProcessStartInfo(installer)
            {
                UseShellExecute = true
            };
            startInfo.ArgumentList.Add("/CURRENTUSER");
            startInfo.ArgumentList.Add("/SILENT");
            startInfo.ArgumentList.Add("/CLOSEAPPLICATIONS");
            Process.Start(startInfo);
            Close();
        }
        catch (Exception exception)
        {
            App.Logger.Error("Update.Install", exception);
            await ShowMessageAsync("Update konnte nicht installiert werden", exception.Message);
            InstallUpdateButton.IsEnabled = true;
            ExportProgressBar.Visibility = Visibility.Collapsed;
            SetBusy(busy: false);
        }
    }

    private static Version CurrentVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 1, 1);
    }

    private void OnOpenLogsClick(object sender, RoutedEventArgs e)
    {
        OpenFolder(App.Paths.Logs);
    }

    private void OnOpenDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        OpenFolder(App.Paths.Diagnostics);
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true
        });
    }

    private void OnClearListClick(object sender, RoutedEventArgs e)
    {
        foreach (ImageAssetViewModel image in Images)
        {
            image.PropertyChanged -= OnAssetPropertyChanged;
        }
        Images.Clear();
        CropEditor.Asset = null;
        PreviewTitle.Text = "Vorschau";
        PreviewSubtitle.Text = "Komplettes Original mit separatem Crop-Overlay";
        UpdateSelectionCount();
        UpdateSummary();
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        foreach (ImageAssetViewModel image in Images)
        {
            image.IsSelected = true;
        }
    }

    private void OnSelectNoneClick(object sender, RoutedEventArgs e)
    {
        foreach (ImageAssetViewModel image in Images)
        {
            image.IsSelected = false;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Bilder zu Image Converter hinzufügen";
            e.DragUIOverride.IsContentVisible = true;
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }
        IReadOnlyList<IStorageItem> obj = await e.DataView.GetStorageItemsAsync();
        List<string> list = new List<string>();
        foreach (IStorageItem item in obj)
        {
            if (!(item is StorageFile storageFile))
            {
                if (item is StorageFolder storageFolder)
                {
                    list.AddRange(EnumerateImageFiles(storageFolder.Path));
                    _lastSourceFolder = storageFolder.Path;
                }
            }
            else
            {
                list.Add(storageFile.Path);
            }
        }
        await ImportPathsAsync(list);
    }

    private async void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
        {
            return;
        }
        switch (e.Key)
        {
            default:
                return;
            case VirtualKey.O:
                OnAddFilesClick(this, new RoutedEventArgs());
                break;
            case VirtualKey.E:
                await StartExportAsync(Images.Where((ImageAssetViewModel x) => x.IsSelected).ToArray());
                break;
            case VirtualKey.A:
                if (FocusManager.GetFocusedElement(RootGrid.XamlRoot) is TextBox)
                {
                    return;
                }
                foreach (ImageAssetViewModel image in Images)
                {
                    image.IsSelected = true;
                }
                break;
            case VirtualKey.Z:
                CropEditor.Undo();
                break;
            case VirtualKey.Y:
                CropEditor.Redo();
                break;
        }
        e.Handled = true;
    }

    private async void OnShowResultsClick(object sender, RoutedEventArgs e)
    {
        IEnumerable<string> values = _lastResults.Select((ExportResultViewModel x) => $"{x.Source} → {x.Destination}\n{x.Status}: {x.Details}");
        await ShowMessageAsync("Exportergebnisse", string.Join("\n\n", values));
    }

    private void UpdateSelectionCount()
    {
        int value = Images.Count((ImageAssetViewModel x) => x.IsSelected);
        SelectionCountText.Text = ((Images.Count == 0) ? "Keine Bilder" : $"{value} von {Images.Count} ausgewählt");
    }

    private void UpdateSummary()
    {
        if (IsControlStateReady())
        {
            ExportProfile profile = BuildProfile();
            OutputSummaryText.Text = DescribeProfile(profile);
            CropEditor.SetTarget(CurrentWidth(), CurrentHeight());
            _estimateCancellation?.Cancel();
            _estimateCancellation?.Dispose();
            _estimateCancellation = null;
            if (CurrentAsset == null)
            {
                OriginalSizeText.Text = "–";
                OriginalDimensionsText.Text = "–";
                OriginalAspectText.Text = "–";
                OutputPixelText.Text = "–";
                EstimatedSizeText.Text = "–";
                EstimateHintText.Text = "Die tatsächliche Größe wird nach dem Export angezeigt.";
                EstimateProgressRing.IsActive = false;
                return;
            }
            OriginalSizeText.Text = CurrentAsset.FileSizeText;
            ExportSizeEstimate exportSizeEstimate = ExportSizeEstimator.Estimate(CurrentAsset.File, profile, CurrentAsset.Crop);
            OutputPixelText.Text = $"{exportSizeEstimate.OutputWidth:N0} × {exportSizeEstimate.OutputHeight:N0}";
            EstimatedSizeText.Text = "wird genauer berechnet …";
            EstimateHintText.Text = exportSizeEstimate.IsUpscaling
                ? $"Achtung: {exportSizeEstimate.UpscaleFactor:0.0}× Hochskalierung. Dadurch kann die Datei trotz Kompression größer werden."
                : "Die Vorschau wird kurz mit denselben Einstellungen testkodiert.";
            _estimateCancellation = new CancellationTokenSource();
            _ = UpdateAccurateEstimateAsync(CurrentAsset, profile, _estimateCancellation.Token);
        }
    }

    private async Task UpdateAccurateEstimateAsync(ImageAssetViewModel asset, ExportProfile profile, CancellationToken cancellationToken)
    {
        try
        {
            EstimateProgressRing.IsActive = true;
            await Task.Delay(300, cancellationToken);
            ExportSizeEstimate estimate = await ExportSizeEstimator.EstimateAccurateAsync(asset.File, profile, asset.Crop, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(CurrentAsset, asset))
            {
                return;
            }
            EstimatedSizeText.Text = "≈ " + ImageAssetViewModel.FormatBytes(estimate.LowerBytes) + "–" + ImageAssetViewModel.FormatBytes(estimate.UpperBytes);
            EstimateHintText.Text = estimate.IsUpscaling
                ? $"Aus einer Testkodierung geschätzt · {estimate.UpscaleFactor:0.0}× Hochskalierung."
                : "Aus einer Testkodierung der Bildvorschau geschätzt; Motivdetails können das Ergebnis noch beeinflussen.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            App.Logger.Error("Export.Estimate", exception, asset.File.Path);
            ExportSizeEstimate fallback = ExportSizeEstimator.Estimate(asset.File, profile, asset.Crop);
            EstimatedSizeText.Text = "≈ " + ImageAssetViewModel.FormatBytes(fallback.LowerBytes) + "–" + ImageAssetViewModel.FormatBytes(fallback.UpperBytes);
            EstimateHintText.Text = "Grobe Schätzung; die Testkodierung war für dieses Bild nicht möglich.";
        }
        finally
        {
            if (_estimateCancellation?.Token == cancellationToken)
            {
                EstimateProgressRing.IsActive = false;
            }
        }
    }

    private bool IsControlStateReady()
    {
        if (ModeCombo.SelectedItem is NamedOption<TransformationMode>
            && FormatCombo.SelectedItem is NamedOption<OutputImageFormat>
            && MetadataCombo.SelectedItem is NamedOption<MetadataMode>
            && TransparencyCombo.SelectedItem is NamedOption<TransparencyMode>)
        {
            return NamingModeCombo.SelectedItem is NamedOption<NamingMode>;
        }
        return false;
    }

    private ExportProfile BuildProfile()
    {
        return new ExportProfile
        {
            Name = ((PresetCombo.SelectedItem is ExportProfile exportProfile) ? exportProfile.Name : "Benutzerdefiniert"),
            Mode = CurrentMode(),
            Width = CurrentWidth(),
            Height = CurrentHeight(),
            Format = ((NamedOption<OutputImageFormat>)FormatCombo.SelectedItem).Value,
            Quality = Math.Clamp((int)Math.Round(QualitySlider.Value), 1, 100),
            PreventUpscale = (PreventUpscaleCheck.IsChecked == true),
            PreserveAspectRatio = (AspectLockCheck.IsChecked == true),
            Metadata = ((NamedOption<MetadataMode>)MetadataCombo.SelectedItem).Value,
            Transparency = ((NamedOption<TransparencyMode>)TransparencyCombo.SelectedItem).Value
        };
    }

    private TransformationMode CurrentMode()
    {
        if (!(ModeCombo.SelectedItem is NamedOption<TransformationMode> namedOption))
        {
            return TransformationMode.OriginalSize;
        }
        return namedOption.Value;
    }

    private NamingMode CurrentNamingMode()
    {
        if (!(NamingModeCombo.SelectedItem is NamedOption<NamingMode> namedOption))
        {
            return NamingMode.KeepOriginal;
        }
        return namedOption.Value;
    }

    private int CurrentWidth()
    {
        return Math.Max(1, double.IsNaN(WidthBox.Value) ? 600 : ((int)Math.Round(WidthBox.Value)));
    }

    private int CurrentHeight()
    {
        return Math.Max(1, double.IsNaN(HeightBox.Value) ? 1050 : ((int)Math.Round(HeightBox.Value)));
    }

    private double CurrentTargetAspect()
    {
        return (double)CurrentWidth() / (double)CurrentHeight();
    }

    private static string DescribeProfile(ExportProfile profile)
    {
        TransformationMode mode = profile.Mode;
        bool flag = (uint)(mode - 4) <= 1u;
        string value = (flag ? "Originalgröße" : $"{profile.Width} × {profile.Height}");
        return $"{value} · {profile.Format} {profile.Quality}";
    }

    private static string FormatRatio(int width, int height)
    {
        int a = Math.Abs(width);
        int b = Math.Abs(height);
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }
        int divisor = Math.Max(1, a);
        return $"{width / divisor}:{height / divisor}";
    }

    private void SetBusy(bool busy, string? text = null)
    {
        BusyRing.IsActive = busy;
        if (text != null)
        {
            StatusText.Text = text;
        }
    }

    private void SetStatus(string text)
    {
        StatusText.Text = text;
    }

    private async Task ShowErrorListAsync(string title, IEnumerable<string> errors)
    {
        await ShowMessageAsync(title, string.Join("\n\n", errors));
    }

    private async Task ShowMessageAsync(string title, string content)
    {
        TextBlock content2 = new TextBlock
        {
            Text = content,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true
        };
        ScrollViewer content3 = new ScrollViewer
        {
            Content = content2,
            MaxHeight = 520.0,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        await new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = title,
            Content = content3,
            CloseButtonText = "Schließen",
            DefaultButton = ContentDialogButton.Close
        }.ShowAsync();
    }

    private async Task<bool> ShowConfirmationAsync(string title, string content, string primaryText)
    {
        TextBlock content2 = new TextBlock
        {
            Text = content,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true
        };
        ScrollViewer content3 = new ScrollViewer
        {
            Content = content2,
            MaxHeight = 520.0,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        return await new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = title,
            Content = content3,
            PrimaryButtonText = primaryText,
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Primary
        }.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowExportSummaryAsync(IReadOnlyList<ExportResult> results)
    {
        int num = results.Count((ExportResult x) => x.Success);
        int num2 = results.Count - num;
        IEnumerable<string> values = from x in results.Take(20)
                                     select x.Success ? $"✓ {x.Job.Source.File.FileName} → {Path.GetFileName(x.Job.DestinationPath)} · {ImageAssetViewModel.FormatBytes(x.OutputBytes)}" : ("Fehler: " + x.Job.Source.File.FileName + " · " + x.ErrorMessage);
        await ShowMessageAsync((num2 == 0) ? "Export abgeschlossen" : "Export mit Fehlern abgeschlossen", $"{num} erfolgreich · {num2} Fehler\n\n" + string.Join("\n", values));
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _operationCancellation?.Cancel();
        _estimateCancellation?.Cancel();
        try
        {
            AppSettings settings = new AppSettings
            {
                Theme = ((ThemeCombo.SelectedItem as string) ?? "System"),
                LastSourceFolder = _lastSourceFolder,
                LastDestinationFolder = _destinationFolder,
                LastOutputFormat = ((FormatCombo.SelectedItem is NamedOption<OutputImageFormat> namedOption) ? namedOption.Value : OutputImageFormat.WebP),
                LastTransformationMode = CurrentMode(),
                LastTransparencyMode = ((TransparencyCombo.SelectedItem is NamedOption<TransparencyMode> transparency) ? transparency.Value : TransparencyMode.Preserve),
                LastMetadataMode = ((MetadataCombo.SelectedItem is NamedOption<MetadataMode> metadata) ? metadata.Value : MetadataMode.RemoveAll),
                LastQuality = (int)Math.Round(QualitySlider.Value),
                LastWidth = CurrentWidth(),
                LastHeight = CurrentHeight(),
                NamingPrefix = NamingPrefixBox.Text,
                NamingStartNumber = (int)NamingStartBox.Value,
                NamingNumberLength = (int)NamingLengthBox.Value,
                ExportParallelism = (int)ParallelismBox.Value,
                RestoreLastSession = RestoreSessionToggle.IsOn,
                CheckForUpdates = AutoUpdateToggle.IsOn,
                LastUpdateCheckUtc = _lastUpdateCheckUtc,
                SelectedImagePath = CurrentAsset?.File.Path,
                SessionImages = RestoreSessionToggle.IsOn
                    ? Images.Select(image => new SessionImageState
                    {
                        Path = image.File.Path,
                        Crop = image.Crop,
                        IsSelected = image.IsSelected,
                        LastExportText = image.LastExportText
                    }).ToList()
                    : []
            };
            await App.Settings.SaveAsync(settings);
        }
        catch (Exception exception)
        {
            App.Logger.Error("Settings.SaveOnClose", exception);
        }
    }

}
