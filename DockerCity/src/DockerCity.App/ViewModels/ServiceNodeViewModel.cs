using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using DockerCity.Domain;
using DockerCity.Domain.Catalog;
using DockerCity.Domain.Layout;
using DockerCity.Domain.Values;

namespace DockerCity.App.ViewModels;

public sealed partial class ServiceNodeViewModel : ObservableObject
{
    private readonly ComposeService _service;

    public ServiceNodeViewModel(ComposeService service, LayoutPoint position, ImageMapping? mapping)
    {
        ArgumentNullException.ThrowIfNull(service);

        _service = service;
        _x = position.X;
        _y = position.Y;

        DisplayName = mapping?.DisplayName ?? service.Image.DisplayName;
        IconFileName = mapping?.IconFileName ?? string.Empty;
    }

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionOpacity))]
    private bool _isSelected;

    // A plain number instead of a Visibility keeps the control free of value
    // converters; the selection ring is always there and simply fades.
    public double SelectionOpacity => IsSelected ? 1 : 0;

    public string Name => _service.Name;

    public string DisplayName { get; }

    // Phase 4 draws a coloured placeholder. Real artwork drops into
    // assets/icons later and this is the name it will be looked up by.
    public string IconFileName { get; }

    public ServiceCategory Category => _service.Category;

    public string ImageLabel => $"{_service.Image.Repository}:{_service.Image.Tag}";

    public string Initials => DisplayName.Length >= 2
        ? DisplayName[..2].ToUpperInvariant()
        : DisplayName.ToUpperInvariant();

    public string Description => _service.Describe();

    public string Badges => string.Join(" · ", BadgeParts());

    // Drawn as a small sentinel on the icon rather than as a text badge.
    public bool IsSupervised => _service.IsSupervised;

    public string SupervisedText => _service.Restart switch
    {
        RestartPolicy.Always => "Sentinel · restart: always",
        RestartPolicy.UnlessStopped => "Sentinel · restart: unless-stopped",
        _ => string.Empty
    };

    // --- Detail panel ---

    public string ContainerText => _service.ContainerName ?? "(derived by Compose)";

    public string CategoryText => _service.Category.ToString();

    public string PortsText => _service.Ports.Count == 0
        ? "-"
        : string.Join("\n", _service.Ports.Select(Describe));

    public string NetworksText => _service.NetworkNames.Count == 0
        ? "default (implicit)"
        : string.Join(", ", _service.NetworkNames);

    public string VolumesText => _service.Volumes.Count == 0
        ? "-"
        : string.Join("\n", _service.Volumes.Select(volume =>
            $"{volume.Source ?? "(anonymous)"} → {volume.Target}{(volume.IsReadOnly ? " [ro]" : string.Empty)}"));

    public string DependsOnText => _service.DependsOn.Count == 0
        ? "-"
        : string.Join(", ", _service.DependsOn);

    public string CommandText => _service.Command is null
        ? "-"
        : _service.Command.Kind == CommandKind.Exec
            ? $"[exec] {string.Join(" ", _service.Command.Arguments)}"
            : $"[shell] {_service.Command.Raw}";

    // Secret-looking values are masked; a dashboard gets screen-shared.
    public string EnvironmentText => _service.Environment.Count == 0
        ? "-"
        : string.Join("\n", _service.Environment.Select(entry => $"{entry.Key} = {entry.DisplayValue}"));

    public string TooltipText
    {
        get
        {
            var text = new StringBuilder();

            text.AppendLine(_service.Name);
            text.AppendLine(ImageLabel);
            text.AppendLine($"container: {ContainerText}");
            text.AppendLine($"networks: {NetworksText}");

            if (_service.Ports.Count > 0)
            {
                text.AppendLine($"ports: {string.Join(", ", _service.Ports.Select(Describe))}");
            }

            if (_service.Volumes.Count > 0)
            {
                text.AppendLine($"volumes: {_service.Volumes.Count}");
            }

            return text.ToString().TrimEnd();
        }
    }

    private static string Describe(PortMapping port)
    {
        var container = port.IsRange
            ? $"{port.ContainerStart}-{port.ContainerEnd}"
            : port.ContainerStart.ToString();

        if (!port.IsPublished)
        {
            return $"{container}/{port.Protocol} (not published)";
        }

        var host = port.HostEnd > port.HostStart
            ? $"{port.HostStart}-{port.HostEnd}"
            : port.HostStart!.Value.ToString();

        return $"{host} → {container}/{port.Protocol}";
    }

    private IEnumerable<string> BadgeParts()
    {
        if (_service is IDataPersisting { HasPersistentData: true })
        {
            yield return "persistent";
        }

        if (_service is IWebAccessible { WebUrl: not null })
        {
            yield return "web";
        }

        if (_service.Image.IsLatest)
        {
            yield return "latest";
        }
    }
}
