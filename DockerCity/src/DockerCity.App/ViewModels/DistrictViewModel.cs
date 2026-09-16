using CommunityToolkit.Mvvm.ComponentModel;
using DockerCity.Domain.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DockerCity.App.ViewModels;

public sealed partial class DistrictViewModel : ObservableObject
{
    public DistrictViewModel(DistrictBounds bounds, int paletteIndex)
    {
        ArgumentNullException.ThrowIfNull(bounds);

        Name = bounds.DistrictName;
        IsImplicit = bounds.IsImplicit;
        IsOverlay = bounds.IsOverlay;

        _x = bounds.X;
        _y = bounds.Y;
        _width = bounds.Width;
        _height = bounds.Height;

        var hue = DistrictPalette.For(paletteIndex, bounds.IsImplicit);

        Stroke = new SolidColorBrush(hue);

        // An overlay sits on top of figures that belong to another district,
        // so it gets an outline only - a fill would muddy what is underneath.
        Fill = IsOverlay
            ? new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
            : new SolidColorBrush(Color.FromArgb(30, hue.R, hue.G, hue.B));

        // Dashed for anything the file did not actually say: the implicit
        // default network, and overlays.
        var dashes = new DoubleCollection();

        if (IsImplicit || IsOverlay)
        {
            dashes.Add(6);
            dashes.Add(4);
        }

        DashArray = dashes;
    }

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private double _height;

    public string Name { get; }

    public bool IsImplicit { get; }

    public bool IsOverlay { get; }

    public string Label => (IsImplicit, IsOverlay) switch
    {
        (true, _) => $"{Name} (implicit)",
        (_, true) => $"{Name} (shared)",
        _ => Name
    };

    public Brush Fill { get; }

    public Brush Stroke { get; }

    public DoubleCollection DashArray { get; }

    // Districts follow their members, so a drag moves the border too.
    public void Apply(DistrictBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);

        X = bounds.X;
        Y = bounds.Y;
        Width = bounds.Width;
        Height = bounds.Height;
    }
}
