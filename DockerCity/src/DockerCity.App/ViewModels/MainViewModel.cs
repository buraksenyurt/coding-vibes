using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DockerCity.App.Services;
using DockerCity.Data.Preferences;
using DockerCity.Domain;
using DockerCity.Domain.Layout;

namespace DockerCity.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly CityWorkspace _workspace;
    private readonly LinkRouter _router = new();

    private CityMap? _map;
    private int _projectId;
    private string? _loadedHash;

    // Set while preferences are being read, so that applying them does not
    // immediately write the same values back.
    private bool _applyingPreferences;

    // Why the database could not be opened, kept so that later actions report
    // the real cause instead of a generic "not initialised".
    private string? _initialisationError;

    public MainViewModel(CityWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _workspace = workspace;
    }

    public ObservableCollection<ServiceNodeViewModel> Nodes { get; } = [];

    public ObservableCollection<DistrictViewModel> Districts { get; } = [];

    public ObservableCollection<LinkViewModel> Links { get; } = [];

    public ObservableCollection<RecentFileViewModel> RecentFiles { get; } = [];

    // Raised synchronously after a load has filled the collections, before
    // anything is awaited, so the view can start animating in the same frame.
    public event EventHandler<CityLoadedEventArgs>? CityLoaded;

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

    // The reverse of depends_on: who would break if this service went away.
    [ObservableProperty]
    private string _neededByText = string.Empty;

    // --- Phase 8: file state ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetailsPanelVisible))]
    [NotifyPropertyChangedFor(nameof(IsMinimapVisible))]
    private bool _hasCity;

    [ObservableProperty]
    private string? _currentPath;

    [ObservableProperty]
    private bool _isFileChangedOnDisk;

    public string CurrentFileName => CurrentPath is null ? string.Empty : Path.GetFileName(CurrentPath);

    // --- Phase 8: preferences ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetailsPanelVisible))]
    private bool _showDetails = true;

    [ObservableProperty]
    private bool _showLinks = true;

    [ObservableProperty]
    private bool _showDistricts = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMinimapVisible))]
    private bool _showMinimap = true;

    [ObservableProperty]
    private bool _reopenLastFile;

    [ObservableProperty]
    private ThemePreference _theme = ThemePreference.System;

    public bool IsDetailsPanelVisible => ShowDetails && HasCity;

    public bool IsMinimapVisible => ShowMinimap && HasCity;

    // --- Phase 9: what fitting to the window and the mini map look at ---

    // The area actually occupied by figures and districts. The canvas is
    // larger (it never shrinks below 800x600), so its size is not this.
    [ObservableProperty]
    private LayoutBounds _contentBounds;

    public WindowPlacement? SavedWindow { get; private set; }

    public int MappingCount => _workspace.MappingCount;

    public string DatabaseFile => _workspace.DatabaseFile;

    public async Task InitialiseAsync()
    {
        try
        {
            await _workspace.InitialiseAsync();
            Status = $"Ready · {_workspace.MappingCount} image mappings loaded.";
        }
        catch (Exception exception)
        {
            _initialisationError = exception.Message;
            Status = $"Database unavailable: {exception.Message}";
            return;
        }

        string? lastFile = null;

        try
        {
            _applyingPreferences = true;

            await _workspace.UsePreferencesAsync(async preferences =>
            {
                Theme = await preferences.GetThemeAsync();
                ShowDetails = await preferences.GetLayerVisibleAsync(AppPreferences.ShowDetailsKey);
                ShowLinks = await preferences.GetLayerVisibleAsync(AppPreferences.ShowLinksKey);
                ShowDistricts = await preferences.GetLayerVisibleAsync(AppPreferences.ShowDistrictsKey);
                ShowMinimap = await preferences.GetLayerVisibleAsync(AppPreferences.ShowMinimapKey);
                ReopenLastFile = await preferences.GetReopenLastFileAsync();
                SavedWindow = await preferences.GetWindowAsync();
                lastFile = await preferences.GetLastFileAsync();
            });
        }
        catch (Exception exception)
        {
            // Preferences are a comfort, not a requirement.
            Status = $"Could not read preferences: {exception.Message}";
        }
        finally
        {
            _applyingPreferences = false;
        }

        await RefreshRecentAsync();

        if (ReopenLastFile && lastFile is not null && File.Exists(lastFile))
        {
            await LoadAsync(lastFile);
        }
    }

    public async Task LoadAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!_workspace.IsInitialised)
        {
            Status = $"Database unavailable: {_initialisationError ?? "still starting up, try again in a moment."}";
            return;
        }

        IsBusy = true;

        try
        {
            var city = await _workspace.LoadAsync(path);
            var isSameFile = string.Equals(CurrentPath, path, StringComparison.OrdinalIgnoreCase) && HasCity;

            _map = city.Map;
            _projectId = city.ProjectId;
            _loadedHash = city.Hash;
            Select(null);

            Nodes.Clear();
            Links.Clear();
            Districts.Clear();

            // Districts are added before the figures so the canvas draws them
            // underneath; see CityCanvas for why order is what decides depth.
            for (var index = 0; index < city.Layout.Districts.Count; index++)
            {
                Districts.Add(new DistrictViewModel(city.Layout.Districts[index], index));
            }

            foreach (var link in city.Map.Links)
            {
                Links.Add(new LinkViewModel(link, _router.Route(link, city.Layout.Nodes)));
            }

            foreach (var service in city.Map.Services)
            {
                Nodes.Add(new ServiceNodeViewModel(
                    service,
                    city.Layout.Nodes[service.Name],
                    city.Catalog.Match(service.Image)));
            }

            ApplyExtent(city.Layout);
            ContentBounds = LayoutBounds.Of(city.Layout);

            CurrentPath = path;
            OnPropertyChanged(nameof(CurrentFileName));
            HasCity = true;
            IsFileChangedOnDisk = false;

            CityLoaded?.Invoke(this, new CityLoadedEventArgs(isSameFile));

            var implicitDistricts = city.Map.Districts.Count(district => district.IsImplicit);

            Status = $"{Nodes.Count} services · {city.Map.Districts.Count} districts "
                   + $"({implicitDistricts} implicit) · {Links.Count} links · {Path.GetFileName(path)}"
                   + (city.ChangedSinceLastVisit ? " · changed since you last opened it" : string.Empty);

            await PersistAsync(preferences => preferences.SetLastFileAsync(path));
            await RefreshRecentAsync();
        }
        catch (Exception exception)
        {
            _map = null;
            Nodes.Clear();
            Links.Clear();
            Districts.Clear();
            HasCity = false;
            Status = $"Could not read {Path.GetFileName(path)}: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ReloadAsync()
    {
        if (CurrentPath is null)
        {
            return;
        }

        if (!File.Exists(CurrentPath))
        {
            Status = $"{Path.GetFileName(CurrentPath)} no longer exists.";
            return;
        }

        await LoadAsync(CurrentPath);
    }

    public async Task OpenRecentAsync(RecentFileViewModel recent)
    {
        ArgumentNullException.ThrowIfNull(recent);

        if (!File.Exists(recent.FilePath))
        {
            // A dead entry is worse than no entry: take it off the list and say why.
            await _workspace.RemoveFromRecentAsync(recent.FilePath);
            await RefreshRecentAsync();
            Status = $"{recent.FilePath} could not be found and was removed from recent files.";
            return;
        }

        await LoadAsync(recent.FilePath);
    }

    public async Task ClearRecentAsync()
    {
        try
        {
            var hidden = await _workspace.ClearRecentAsync();
            await RefreshRecentAsync();
            Status = $"Cleared {hidden} recent files. Stored layouts were kept.";
        }
        catch (Exception exception)
        {
            Status = $"Could not clear recent files: {exception.Message}";
        }
    }

    public async Task RefreshRecentAsync()
    {
        if (!_workspace.IsInitialised)
        {
            return;
        }

        try
        {
            var recent = await _workspace.RecentAsync();

            RecentFiles.Clear();

            foreach (var project in recent)
            {
                RecentFiles.Add(new RecentFileViewModel(project.Name, project.FilePath, project.LastOpenedAt));
            }
        }
        catch (Exception exception)
        {
            Status = $"Could not read recent files: {exception.Message}";
        }
    }

    // Called by the file watcher. Editors often touch a file without changing
    // it, so the content is compared rather than trusting the event alone.
    public async Task CheckFileOnDiskAsync()
    {
        if (CurrentPath is null || _loadedHash is null)
        {
            return;
        }

        try
        {
            var hash = await CityWorkspace.HashFileAsync(CurrentPath);
            IsFileChangedOnDisk = hash != _loadedHash;
        }
        catch (IOException)
        {
            // Still being written; the next event will try again.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public Task SaveWindowAsync(WindowPlacement placement) =>
        PersistAsync(preferences => preferences.SetWindowAsync(placement));

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

        foreach (var link in Links)
        {
            link.IsHighlighted = node is not null && link.Touches(node.Name);
            link.IsDimmed = node is not null && !link.IsHighlighted;
        }

        if (node is null)
        {
            NeededByText = string.Empty;
            return;
        }

        var neededBy = Links
            .Where(link => link.IsDirected && link.To == node.Name)
            .Select(link => link.From)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        NeededByText = neededBy.Count == 0 ? "-" : string.Join(", ", neededBy);
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

        // Roads are cheap to recompute: a handful of square roots per link.
        foreach (var link in Links)
        {
            link.Route = _router.Route(link.Link, positions);
        }

        ApplyExtent(layout);
        ContentBounds = LayoutBounds.Of(layout);
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

    // --- Preferences are written as soon as they change ---

    partial void OnShowDetailsChanged(bool value) =>
        _ = PersistAsync(preferences => preferences.SetLayerVisibleAsync(AppPreferences.ShowDetailsKey, value));

    partial void OnShowLinksChanged(bool value) =>
        _ = PersistAsync(preferences => preferences.SetLayerVisibleAsync(AppPreferences.ShowLinksKey, value));

    partial void OnShowDistrictsChanged(bool value) =>
        _ = PersistAsync(preferences => preferences.SetLayerVisibleAsync(AppPreferences.ShowDistrictsKey, value));

    partial void OnShowMinimapChanged(bool value) =>
        _ = PersistAsync(preferences => preferences.SetLayerVisibleAsync(AppPreferences.ShowMinimapKey, value));

    partial void OnReopenLastFileChanged(bool value) =>
        _ = PersistAsync(preferences => preferences.SetReopenLastFileAsync(value));

    partial void OnThemeChanged(ThemePreference value) =>
        _ = PersistAsync(preferences => preferences.SetThemeAsync(value));

    private async Task PersistAsync(Func<AppPreferences, Task> write)
    {
        // Before the database is open (or if it never opens) there is nowhere
        // to write. Preferences are a comfort: skip quietly rather than
        // replace a status message that explains the real problem.
        if (_applyingPreferences || !_workspace.IsInitialised)
        {
            return;
        }

        try
        {
            await _workspace.UsePreferencesAsync(write);
        }
        catch (Exception exception)
        {
            Status = $"Could not save preferences: {exception.Message}";
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
