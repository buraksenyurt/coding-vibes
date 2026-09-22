using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using DockerCity.App.Services;
using DockerCity.App.ViewModels;
using DockerCity.App.Views;
using DockerCity.Data.Preferences;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

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
        CityBoard.NodeMoving += (_, _) => _viewModel.NodeMoved();
        CityBoard.NodeDropped += async (_, node) => await _viewModel.NodeDroppedAsync(node);

        // Clicking empty canvas clears the selection.
        CityBoard.PointerPressed += (_, _) => _viewModel.Select(null);

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

    private async void OnResetClick(object sender, RoutedEventArgs args) => await _viewModel.ResetLayoutAsync();

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
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(CityFrame);
            var pixels = await bitmap.GetPixelsAsync();

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
