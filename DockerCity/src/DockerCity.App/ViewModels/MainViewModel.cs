using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DockerCity.App.Services;

namespace DockerCity.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly CityWorkspace _workspace;

    public MainViewModel(CityWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _workspace = workspace;
    }

    public ObservableCollection<ServiceNodeViewModel> Nodes { get; } = [];

    [ObservableProperty]
    private string _status = "Open a docker-compose file to build the city.";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _canvasWidth = 800;

    [ObservableProperty]
    private double _canvasHeight = 600;

    public async Task InitialiseAsync()
    {
        try
        {
            await _workspace.InitialiseAsync();
            Status = $"Ready · {_workspace.MappingCount} image mappings loaded.";
        }
        catch (Exception exception)
        {
            Status = $"Database unavailable: {exception.Message}";
        }
    }

    public async Task LoadAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        IsBusy = true;

        try
        {
            var city = await Task.Run(() => _workspace.Load(path));

            Nodes.Clear();

            foreach (var service in city.Map.Services)
            {
                Nodes.Add(new ServiceNodeViewModel(
                    service,
                    city.Layout.Nodes[service.Name],
                    city.Catalog.Match(service.Image)));
            }

            CanvasWidth = city.Layout.Width;
            CanvasHeight = city.Layout.Height;

            var implicitDistricts = city.Map.Districts.Count(district => district.IsImplicit);

            Status = $"{Nodes.Count} services · {city.Map.Districts.Count} districts "
                   + $"({implicitDistricts} implicit) · {Path.GetFileName(path)}";
        }
        catch (Exception exception)
        {
            Nodes.Clear();
            Status = $"Could not read {Path.GetFileName(path)}: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
