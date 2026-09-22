using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using DockerCity.App.Services;
using DockerCity.App.ViewModels;
using DockerCity.App.Views;
using DockerCity.Data.Preferences;
using DockerCity.Domain.Layout;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

// Windows.System is not imported whole: it has its own DispatcherQueue types,
// which would clash with the Microsoft.UI.Dispatching ones used here.
using VirtualKey = Windows.System.VirtualKey;
using VirtualKeyModifiers = Windows.System.VirtualKeyModifiers;

namespace DockerCity.App;

public sealed partial class MainWindow : Window
{
    private static readonly string[] ComposeExtensions = [".yml", ".yaml"];

    private readonly CityWorkspace _workspace = new();
    private readonly MainViewModel _viewModel;
    private readonly ComposeFileWatcher _watcher;
    private readonly DispatcherQueueTimer _placementTimer;

    // The last size and position while the window was neither maximized nor
    // minimized. A maximized window reports the screen as its size, which is
    // not what should come back when it is restored.
    private RectInt32? _normalBounds;
    private bool _dialogOpen;

    // Panning by dragging empty ground. Positions are measured against the
    // ScrollViewer, which stays put, not against the canvas, which moves
    // under the pointer as soon as the view scrolls.
    private bool _panning;
    private Point _panStart;
    private double _panOriginX;
    private double _panOriginY;

    public MainWindow()
    {
        InitializeComponent();
        Title = "DockerCity";

        _viewModel = new MainViewModel(_workspace);
        _watcher = new ComposeFileWatcher(DispatcherQueue);

        RootGrid.DataContext = _viewModel;
        CityBoard.Districts = _viewModel.Districts;
        CityBoard.Links = _viewModel.Links;
        CityBoard.Nodes = _viewModel.Nodes;

        CityBoard.NodeSelected += (_, node) => _viewModel.Select(node);
        CityBoard.NodeDropped += async (_, node) => await _viewModel.NodeDroppedAsync(node);
        CityBoard.NodeMoving += (_, _) =>
        {
            _viewModel.NodeMoved();
            RenderMinimap();
        };

        // Empty ground: a click clears the selection, a drag pans the city.
        CityBoard.PointerPressed += OnBoardPointerPressed;
        CityBoard.PointerMoved += OnBoardPointerMoved;
        CityBoard.PointerReleased += OnBoardPointerReleased;
        CityBoard.PointerCaptureLost += (_, _) => EndPan();

        CityScroller.ViewChanged += (_, _) => UpdateViewIndicators();
        CityScroller.SizeChanged += (_, _) => UpdateViewIndicators();
        Minimap.NavigateRequested += (_, point) => CenterOn(point);
        _viewModel.CityLoaded += OnCityLoaded;
        AddZoomAccelerators();

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.RecentFiles.CollectionChanged += (_, _) => RebuildRecentMenu();
        _watcher.Changed += async (_, _) => await _viewModel.CheckFileOnDiskAsync();

        // Window moves arrive in bursts while dragging the title bar. Saving
        // is postponed until the window has been still for a moment.
        _placementTimer = DispatcherQueue.CreateTimer();
        _placementTimer.Interval = TimeSpan.FromMilliseconds(600);
        _placementTimer.IsRepeating = false;
        _placementTimer.Tick += async (_, _) => await SavePlacementAsync();
        AppWindow.Changed += OnAppWindowChanged;

        Closed += (_, _) =>
        {
            _placementTimer.Stop();
            _watcher.Dispose();
            _workspace.Dispose();
        };

        RebuildRecentMenu();
        SyncMenuState();

        _ = StartAsync();
    }

    private async Task StartAsync()
    {
        await _viewModel.InitialiseAsync();

        RestorePlacement(_viewModel.SavedWindow);
        ApplyTheme(_viewModel.Theme);
        SyncMenuState();
    }

    // ----------------------------------------------------------- File menu

    private async void OnOpenClick(object sender, RoutedEventArgs args) => await OpenWithPickerAsync();

    private async void OnReloadClick(object sender, RoutedEventArgs args) => await _viewModel.ReloadAsync();

    private async void OnExportClick(object sender, RoutedEventArgs args) => await ExportPngAsync();

    private void OnReopenLastClick(object sender, RoutedEventArgs args) =>
        _viewModel.ReopenLastFile = ReopenLastItem.IsChecked;

    private void OnExitClick(object sender, RoutedEventArgs args) => Close();

    private async void OnRecentItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is RecentFileViewModel recent)
        {
            await _viewModel.OpenRecentAsync(recent);
        }
    }

    private async Task OpenWithPickerAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder
        };

        // A picker in a desktop app has no window of its own to sit on top of,
        // so it has to be told which one owns it. Packaged or not, this is
        // required in WinUI desktop.
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        foreach (var extension in ComposeExtensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

        var file = await picker.PickSingleFileAsync();

        if (file is not null)
        {
            await _viewModel.LoadAsync(file.Path);
        }
    }

    private void RebuildRecentMenu()
    {
        RecentMenu.Items.Clear();

        if (_viewModel.RecentFiles.Count == 0)
        {
            RecentMenu.Items.Add(new MenuFlyoutItem { Text = "No recent files", IsEnabled = false });
            return;
        }

        foreach (var recent in _viewModel.RecentFiles)
        {
            var item = new MenuFlyoutItem { Text = recent.MenuText };
            item.Click += async (_, _) => await _viewModel.OpenRecentAsync(recent);
            RecentMenu.Items.Add(item);
        }

        RecentMenu.Items.Add(new MenuFlyoutSeparator());

        var clear = new MenuFlyoutItem { Text = "Clear recent files" };
        clear.Click += async (_, _) => await _viewModel.ClearRecentAsync();
        RecentMenu.Items.Add(clear);
    }

    // ----------------------------------------------------------- View menu

    private void OnToggleDetailsClick(object sender, RoutedEventArgs args) => _viewModel.ShowDetails = DetailsItem.IsChecked;

    private void OnToggleLinksClick(object sender, RoutedEventArgs args) => _viewModel.ShowLinks = LinksItem.IsChecked;

    private void OnToggleDistrictsClick(object sender, RoutedEventArgs args) => _viewModel.ShowDistricts = DistrictsItem.IsChecked;

    private void OnToggleMinimapClick(object sender, RoutedEventArgs args) => _viewModel.ShowMinimap = MinimapItem.IsChecked;

    private async void OnResetClick(object sender, RoutedEventArgs args) => await _viewModel.ResetLayoutAsync();

    private void OnZoomInClick(object sender, RoutedEventArgs args) => ZoomIn();

    private void OnZoomOutClick(object sender, RoutedEventArgs args) => ZoomOut();

    private void OnActualSizeClick(object sender, RoutedEventArgs args) => ZoomTo(1);

    private void OnFitClick(object sender, RoutedEventArgs args) => FitToWindow(animate: true);

    private void OnThemeClick(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<ThemePreference>(tag, out var theme))
        {
            _viewModel.Theme = theme;
        }
    }

    private void ApplyTheme(ThemePreference theme) =>
        RootGrid.RequestedTheme = theme switch
        {
            ThemePreference.Light => ElementTheme.Light,
            ThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

    // Menu items live in flyouts outside the visual tree, where bindings to the
    // window's DataContext are unreliable. Their checked state is set by hand.
    private void SyncMenuState()
    {
        ReopenLastItem.IsChecked = _viewModel.ReopenLastFile;
        DetailsItem.IsChecked = _viewModel.ShowDetails;
        LinksItem.IsChecked = _viewModel.ShowLinks;
        DistrictsItem.IsChecked = _viewModel.ShowDistricts;
        MinimapItem.IsChecked = _viewModel.ShowMinimap;

        ThemeSystemItem.IsChecked = _viewModel.Theme == ThemePreference.System;
        ThemeLightItem.IsChecked = _viewModel.Theme == ThemePreference.Light;
        ThemeDarkItem.IsChecked = _viewModel.Theme == ThemePreference.Dark;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(MainViewModel.Theme):
                ApplyTheme(_viewModel.Theme);
                SyncMenuState();
                break;

            case nameof(MainViewModel.ShowDetails):
            case nameof(MainViewModel.ShowLinks):
            case nameof(MainViewModel.ShowDistricts):
            case nameof(MainViewModel.ReopenLastFile):
                SyncMenuState();
                break;

            case nameof(MainViewModel.ShowMinimap):
                SyncMenuState();
                UpdateViewIndicators();
                break;

            case nameof(MainViewModel.CurrentPath):
                if (_viewModel.CurrentPath is { } path)
                {
                    _watcher.Watch(path);
                    Title = $"{Path.GetFileName(path)} — DockerCity";
                }
                break;
        }
    }

    // ----------------------------------------------------------- Help menu

    private async void OnShortcutsClick(object sender, RoutedEventArgs args) =>
        await ShowDialogAsync(root => Dialogs.ShowShortcutsAsync(root));

    private async void OnAboutClick(object sender, RoutedEventArgs args) =>
        await ShowDialogAsync(root => Dialogs.ShowAboutAsync(root, _viewModel.MappingCount, _viewModel.DatabaseFile));

    // Only one ContentDialog may be open at a time; a second ShowAsync throws.
    private async Task ShowDialogAsync(Func<XamlRoot, Task> show)
    {
        if (_dialogOpen || Content.XamlRoot is not { } root)
        {
            return;
        }

        _dialogOpen = true;

        try
        {
            await show(root);
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    // ----------------------------------------------------------- Shortcuts

    private async void OnOpenAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await OpenWithPickerAsync();
    }

    private async void OnReloadAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await _viewModel.ReloadAsync();
    }

    private async void OnExportAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ExportPngAsync();
    }

    private async void OnResetAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await _viewModel.ResetLayoutAsync();
    }

    private void OnToggleDetailsAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _viewModel.ShowDetails = !_viewModel.ShowDetails;
    }

    private void OnToggleLinksAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _viewModel.ShowLinks = !_viewModel.ShowLinks;
    }

    private void OnToggleDistrictsAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _viewModel.ShowDistricts = !_viewModel.ShowDistricts;
    }

    private void OnEscapeAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _viewModel.Select(null);
    }

    private async void OnShortcutsAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ShowDialogAsync(root => Dialogs.ShowShortcutsAsync(root));
    }

    // Key names such as "Add" or "Number0" work in XAML, but the Plus and
    // Minus keys of the main keyboard are OEM keys (187 and 189) with no
    // VirtualKey name. Keeping all the zoom shortcuts together in code is
    // clearer than splitting them between two files.
    private void AddZoomAccelerators()
    {
        const VirtualKey oemPlus = (VirtualKey)187;
        const VirtualKey oemMinus = (VirtualKey)189;

        AddAccelerator(oemPlus, ZoomIn);
        AddAccelerator(VirtualKey.Add, ZoomIn);
        AddAccelerator(oemMinus, ZoomOut);
        AddAccelerator(VirtualKey.Subtract, ZoomOut);
        AddAccelerator(VirtualKey.Number0, () => ZoomTo(1));
        AddAccelerator(VirtualKey.NumberPad0, () => ZoomTo(1));
        AddAccelerator(VirtualKey.Number9, () => FitToWindow(animate: true));
        AddAccelerator(VirtualKey.Number4, () => _viewModel.ShowMinimap = !_viewModel.ShowMinimap);
    }

    private void AddAccelerator(VirtualKey key, Action action)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = VirtualKeyModifiers.Control };

        accelerator.Invoked += (_, args) =>
        {
            args.Handled = true;
            action();
        };

        RootGrid.KeyboardAccelerators.Add(accelerator);
    }

    // ----------------------------------------------------------- Zoom and pan

    private void ZoomIn() => ZoomTo(ZoomMath.StepIn(CityScroller.ZoomFactor));

    private void ZoomOut() => ZoomTo(ZoomMath.StepOut(CityScroller.ZoomFactor));

    // ChangeView takes the new offsets in zoomed pixels. Passing the old ones
    // would zoom around the top left corner; ZoomMath keeps the middle of the
    // window where it was.
    private void ZoomTo(double zoom)
    {
        if (!_viewModel.HasCity)
        {
            return;
        }

        var target = ZoomMath.Clamp(zoom);

        var (x, y) = ZoomMath.OffsetsKeepingCenter(
            CityScroller.HorizontalOffset,
            CityScroller.VerticalOffset,
            CityScroller.ViewportWidth,
            CityScroller.ViewportHeight,
            CityScroller.ZoomFactor,
            target);

        CityScroller.ChangeView(x, y, (float)target, disableAnimation: !Motion.IsEnabled);
    }

    private void FitToWindow(bool animate)
    {
        var content = _viewModel.ContentBounds;

        if (!_viewModel.HasCity || content.IsEmpty)
        {
            return;
        }

        var zoom = ZoomMath.Fit(content, CityScroller.ViewportWidth, CityScroller.ViewportHeight);
        var (x, y) = ZoomMath.OffsetsToCenterOn(content.Center, CityScroller.ViewportWidth, CityScroller.ViewportHeight, zoom);

        CityScroller.ChangeView(x, y, (float)zoom, disableAnimation: !animate || !Motion.IsEnabled);
    }

    // The mini map asks for a point to be in the middle; the zoom stays.
    private void CenterOn(LayoutPoint point)
    {
        var (x, y) = ZoomMath.OffsetsToCenterOn(
            point,
            CityScroller.ViewportWidth,
            CityScroller.ViewportHeight,
            CityScroller.ZoomFactor);

        CityScroller.ChangeView(x, y, null, disableAnimation: true);
    }

    private void OnCityLoaded(object? sender, CityLoadedEventArgs args)
    {
        RenderMinimap();

        // A reload keeps the view where the user left it.
        if (args.IsSameFile)
        {
            return;
        }

        CityBoard.PlayEntrance();

        // The ScrollViewer was collapsed until a moment ago and has not been
        // measured yet: its viewport reads 0x0. Low priority runs this after
        // the layout pass, when the numbers are real.
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => FitToWindow(animate: false));
    }

    private void UpdateViewIndicators()
    {
        var zoom = CityScroller.ZoomFactor;

        ZoomButton.Content = $"{Math.Round(zoom * 100)}%";

        Minimap.ShowViewport(
            CityScroller.HorizontalOffset / zoom,
            CityScroller.VerticalOffset / zoom,
            CityScroller.ViewportWidth / zoom,
            CityScroller.ViewportHeight / zoom);
    }

    private void RenderMinimap()
    {
        Minimap.Render(_viewModel.Districts, _viewModel.Nodes, _viewModel.ContentBounds);
        UpdateViewIndicators();
    }

    private void OnBoardPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        _viewModel.Select(null);

        // Touch and pen already pan the ScrollViewer natively; only the mouse
        // needs help.
        if (args.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
        {
            return;
        }

        var point = args.GetCurrentPoint(CityScroller);

        if (!point.Properties.IsLeftButtonPressed && !point.Properties.IsMiddleButtonPressed)
        {
            return;
        }

        _panStart = point.Position;
        _panOriginX = CityScroller.HorizontalOffset;
        _panOriginY = CityScroller.VerticalOffset;
        _panning = CityBoard.CapturePointer(args.Pointer);

        if (_panning)
        {
            CityBoard.SetPanCursor(true);
        }

        args.Handled = true;
    }

    private void OnBoardPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (!_panning)
        {
            return;
        }

        var position = args.GetCurrentPoint(CityScroller).Position;

        // Offsets are in zoomed pixels and so is the pointer, so a pixel of
        // mouse movement is a pixel of scrolling at every zoom level.
        CityScroller.ChangeView(
            _panOriginX - (position.X - _panStart.X),
            _panOriginY - (position.Y - _panStart.Y),
            null,
            disableAnimation: true);

        args.Handled = true;
    }

    private void OnBoardPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (_panning)
        {
            CityBoard.ReleasePointerCapture(args.Pointer);
        }

        EndPan();
    }

    private void EndPan()
    {
        if (!_panning)
        {
            return;
        }

        _panning = false;
        CityBoard.SetPanCursor(false);
    }

    // ----------------------------------------------------------- Drag and drop

    private void OnDragOver(object sender, DragEventArgs args)
    {
        if (args.DataView.Contains(StandardDataFormats.StorageItems))
        {
            args.AcceptedOperation = DataPackageOperation.Copy;
            args.DragUIOverride.Caption = "Open in DockerCity";
        }
    }

    private async void OnDrop(object sender, DragEventArgs args)
    {
        if (!args.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await args.DataView.GetStorageItemsAsync();

        var compose = items
            .OfType<StorageFile>()
            .FirstOrDefault(file => ComposeExtensions.Contains(Path.GetExtension(file.Path), StringComparer.OrdinalIgnoreCase));

        if (compose is null)
        {
            _viewModel.Status = "Only .yml and .yaml files can be opened.";
            return;
        }

        await _viewModel.LoadAsync(compose.Path);
    }

    // ----------------------------------------------------------- Export

    private async Task ExportPngAsync()
    {
        if (!_viewModel.HasCity)
        {
            _viewModel.Status = "Open a compose file before exporting.";
            return;
        }

        try
        {
            var (bitmap, pixels) = await RenderCityAsync();

            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                SuggestedFileName = $"{Path.GetFileNameWithoutExtension(_viewModel.CurrentFileName)}-city"
            };

            picker.FileTypeChoices.Add("PNG image", new List<string> { ".png" });
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

            var file = await picker.PickSaveFileAsync();

            if (file is null)
            {
                return;
            }

            using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);

            // Overwriting a larger file would otherwise leave its tail behind.
            stream.Size = 0;

            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            var dpi = 96 * (Content.XamlRoot?.RasterizationScale ?? 1);

            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                (uint)bitmap.PixelWidth,
                (uint)bitmap.PixelHeight,
                dpi,
                dpi,
                pixels.ToArray());

            await encoder.FlushAsync();

            _viewModel.Status = $"Exported {bitmap.PixelWidth}×{bitmap.PixelHeight} to {file.Path}";
        }
        catch (Exception exception)
        {
            _viewModel.Status = $"Could not export: {exception.Message}";
        }
    }

    // RenderTargetBitmap captures what the compositor already has, and under
    // zoom that is the city rasterized at the zoomed size. At 49% the PNG came
    // out at full size but built from half the pixels: blurry. So the view
    // goes to 100% for the capture and comes straight back.
    private async Task<(RenderTargetBitmap Bitmap, Windows.Storage.Streams.IBuffer Pixels)> RenderCityAsync()
    {
        var zoom = CityScroller.ZoomFactor;
        var offsetX = CityScroller.HorizontalOffset;
        var offsetY = CityScroller.VerticalOffset;
        var zoomed = Math.Abs(zoom - 1) > 0.001f;

        if (zoomed)
        {
            await ChangeViewAsync(0, 0, 1f);
        }

        try
        {
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(CityFrame);
            return (bitmap, await bitmap.GetPixelsAsync());
        }
        finally
        {
            if (zoomed)
            {
                CityScroller.ChangeView(offsetX, offsetY, zoom, disableAnimation: true);
            }
        }
    }

    // ChangeView only asks; the view settles a little later. Wait for the
    // final ViewChanged, then for two frames so the content is rasterized
    // again at the new scale before anything reads it.
    private async Task ChangeViewAsync(double? offsetX, double? offsetY, float zoom)
    {
        var settled = new TaskCompletionSource();

        void OnViewChanged(object? sender, ScrollViewerViewChangedEventArgs args)
        {
            if (!args.IsIntermediate)
            {
                settled.TrySetResult();
            }
        }

        CityScroller.ViewChanged += OnViewChanged;

        try
        {
            if (CityScroller.ChangeView(offsetX, offsetY, zoom, disableAnimation: true))
            {
                // A safety net: never hang an export on an event that did not come.
                await Task.WhenAny(settled.Task, Task.Delay(500));
            }
        }
        finally
        {
            CityScroller.ViewChanged -= OnViewChanged;
        }

        await NextFrameAsync();
        await NextFrameAsync();
    }

    private static Task NextFrameAsync()
    {
        var frame = new TaskCompletionSource();

        void OnRendering(object? sender, object args)
        {
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= OnRendering;
            frame.TrySetResult();
        }

        Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnRendering;
        return frame.Task;
    }

    // ----------------------------------------------------------- Window placement

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidPositionChange && !args.DidSizeChange && !args.DidPresenterChange)
        {
            return;
        }

        if (sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
        {
            _normalBounds = new RectInt32(sender.Position.X, sender.Position.Y, sender.Size.Width, sender.Size.Height);
        }

        _placementTimer.Stop();
        _placementTimer.Start();
    }

    private async Task SavePlacementAsync()
    {
        if (AppWindow.Presenter is not OverlappedPresenter presenter
            || presenter.State == OverlappedPresenterState.Minimized
            || _normalBounds is not { } bounds)
        {
            return;
        }

        await _viewModel.SaveWindowAsync(new WindowPlacement(
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            presenter.State == OverlappedPresenterState.Maximized));
    }

    private void RestorePlacement(WindowPlacement? placement)
    {
        if (placement is null)
        {
            return;
        }

        var bounds = new RectInt32(placement.X, placement.Y, placement.Width, placement.Height);

        // A monitor that was there last time may be gone now. Restoring onto
        // it would put the window somewhere nobody can reach.
        if (DisplayArea.GetFromRect(bounds, DisplayAreaFallback.None) is null)
        {
            return;
        }

        AppWindow.MoveAndResize(bounds);
        _normalBounds = bounds;

        if (placement.IsMaximized && AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
    }
}
