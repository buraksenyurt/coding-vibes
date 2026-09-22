using DockerCity.App.Services;
using DockerCity.App.ViewModels;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DockerCity.App;

public sealed partial class MainWindow : Window
{
    private readonly CityWorkspace _workspace = new();
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Title = "DockerCity";

        _viewModel = new MainViewModel(_workspace);

        RootGrid.DataContext = _viewModel;
        CityBoard.Districts = _viewModel.Districts;
        CityBoard.Links = _viewModel.Links;
        CityBoard.Nodes = _viewModel.Nodes;

        CityBoard.NodeSelected += (_, node) => _viewModel.Select(node);
        CityBoard.NodeMoving += (_, _) => _viewModel.NodeMoved();
        CityBoard.NodeDropped += async (_, node) => await _viewModel.NodeDroppedAsync(node);

        // Clicking empty canvas clears the selection.
        CityBoard.PointerPressed += (_, _) => _viewModel.Select(null);

        Closed += (_, _) => _workspace.Dispose();

        _ = _viewModel.InitialiseAsync();
    }

    private async void OnOpenClick(object sender, RoutedEventArgs args)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder
        };

        // A picker in a desktop app has no window of its own to sit on top of,
        // so it has to be told which one owns it. Packaged or not, this is
        // required in WinUI desktop.
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        picker.FileTypeFilter.Add(".yml");
        picker.FileTypeFilter.Add(".yaml");

        var file = await picker.PickSingleFileAsync();

        if (file is not null)
        {
            await _viewModel.LoadAsync(file.Path);
        }
    }

    private async void OnResetClick(object sender, RoutedEventArgs args) =>
        await _viewModel.ResetLayoutAsync();
}
