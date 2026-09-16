using CommunityToolkit.Mvvm.ComponentModel;
using DockerCity.Domain;
using DockerCity.Domain.Catalog;
using DockerCity.Domain.Layout;

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

    private IEnumerable<string> BadgeParts()
    {
        if (_service.IsSupervised)
        {
            yield return "restarts";
        }

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
