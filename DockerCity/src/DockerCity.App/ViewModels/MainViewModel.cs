using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DockerCity.App.Services;
using DockerCity.Domain;
using DockerCity.Domain.Layout;

namespace DockerCity.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly CityWorkspace _workspace;

    private CityMap? _map;
    private int _projectId;

    public MainViewModel(CityWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _workspace = workspace;
    }

    public ObservableCollection<ServiceNodeViewModel> Nodes { get; } = [];

    public ObservableCollection<DistrictViewModel> Districts { get; } = [];

    [ObservableProperty]
    private string _status = "Open a docker-compose file to build the city.";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _canvasWidth = 800;

    [ObservableProperty]
    private double _canvasHeight = 600;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionTitle))]
    private ServiceNodeViewModel? _selected;

    public string SelectionTitle => Selected?.Name ?? "No service selected";

    public bool CanReset => _map is not null;

    public string? CurrentPath { get; private set; }

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
            var city = await _workspace.LoadAsync(path);

            _map = city.Map;
            _projectId = city.ProjectId;
            CurrentPath = path;
            Select(null);

            Nodes.Clear();
            Districts.Clear();

            // Districts are added before the figures so the canvas draws them
            // underneath; see CityCanvas for why order is what decides depth.
            for (var index = 0; index < city.Layout.Districts.Count; index++)
            {
                Districts.Add(new DistrictViewModel(city.Layout.Districts[index], index));
            }

            foreach (var service in city.Map.Services)
            {
                Nodes.Add(new ServiceNodeViewModel(
                    service,
                    city.Layout.Nodes[service.Name],
                    city.Catalog.Match(service.Image)));
            }

            ApplyExtent(city.Layout);

            var implicitDistricts = city.Map.Districts.Count(district => district.IsImplicit);

            Status = $"{Nodes.Count} services · {city.Map.Districts.Count} districts "
                   + $"({implicitDistricts} implicit) · {Path.GetFileName(path)}";
        }
        catch (Exception exception)
        {
            _map = null;
            Nodes.Clear();
            Districts.Clear();
            Status = $"Could not read {Path.GetFileName(path)}: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Select(ServiceNodeViewModel? node)
    {
        if (ReferenceEquals(Selected, node))
        {
            return;
        }

        if (Selected is not null)
        {
            Selected.IsSelected = false;
        }

        Selected = node;

        if (node is not null)
        {
            node.IsSelected = true;
        }
    }

    // Called continuously while a figure is being dragged. Only the regions
    // are recalculated; the figure moved itself.
    public void NodeMoved()
    {
        if (_map is null)
        {
            return;
        }

        var positions = Nodes.ToDictionary(
            node => node.Name,
            node => new LayoutPoint(node.X, node.Y),
            StringComparer.Ordinal);

        var layout = _workspace.Rebound(_map, positions);

        for (var index = 0; index < Districts.Count; index++)
        {
            var bounds = layout.Districts.FirstOrDefault(
                area => area.DistrictName == Districts[index].Name);

            if (bounds is not null)
            {
                Districts[index].Apply(bounds);
            }
        }

        ApplyExtent(layout);
    }

    public async Task NodeDroppedAsync(ServiceNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (_map is null)
        {
            return;
        }

        try
        {
            await _workspace.SaveLayoutAsync(_projectId, node.Name, node.X, node.Y);
            Status = $"Saved position of {node.Name}.";
        }
        catch (Exception exception)
        {
            Status = $"Could not save position: {exception.Message}";
        }
    }

    public async Task ResetLayoutAsync()
    {
        if (_map is null || CurrentPath is null)
        {
            return;
        }

        try
        {
            var removed = await _workspace.ClearLayoutAsync(_projectId);
            await LoadAsync(CurrentPath);

            Status = removed == 0
                ? "Layout was already the computed one."
                : $"Cleared {removed} stored positions.";
        }
        catch (Exception exception)
        {
            Status = $"Could not reset the layout: {exception.Message}";
        }
    }

    private void ApplyExtent(CityLayout layout)
    {
        // Never shrink below the viewport, otherwise the canvas jumps about
        // while a figure is dragged towards the top left.
        CanvasWidth = Math.Max(layout.Width, 800);
        CanvasHeight = Math.Max(layout.Height, 600);
    }
}
