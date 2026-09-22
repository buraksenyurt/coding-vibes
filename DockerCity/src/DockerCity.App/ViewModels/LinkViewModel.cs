using CommunityToolkit.Mvvm.ComponentModel;
using DockerCity.Domain;
using DockerCity.Domain.Layout;

namespace DockerCity.App.ViewModels;

public sealed partial class LinkViewModel : ObservableObject
{
    public LinkViewModel(CityLink link, LinkPath? route)
    {
        ArgumentNullException.ThrowIfNull(link);

        Link = link;
        _route = route;
    }

    public CityLink Link { get; }

    public string From => Link.From;

    public string To => Link.To;

    // depends_on has a direction and gets an arrow; a shared volume does not.
    public bool IsDirected => Link.Kind == CityLinkKind.DependsOn;

    // Null when the two figures overlap and there is no room for a road.
    [ObservableProperty]
    private LinkPath? _route;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Thickness), nameof(Opacity))]
    private bool _isHighlighted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Thickness), nameof(Opacity))]
    private bool _isDimmed;

    public double Thickness => IsHighlighted ? 3 : 1.5;

    // With something selected, unrelated roads fade so the relevant ones
    // stand out even on a busy map.
    public double Opacity => (IsHighlighted, IsDimmed) switch
    {
        (true, _) => 1,
        (_, true) => 0.2,
        _ => 0.75
    };

    public bool Touches(string serviceName) =>
        string.Equals(From, serviceName, StringComparison.Ordinal)
        || string.Equals(To, serviceName, StringComparison.Ordinal);
}
