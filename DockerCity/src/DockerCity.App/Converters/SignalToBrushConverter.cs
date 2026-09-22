using DockerCity.Domain.Runtime;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DockerCity.App.Converters;

// The runtime traffic light. Green and red are the obvious pair; amber is for
// "not yet" (starting, restarting), and grey for a container that exists but
// is not serving.
public sealed class SignalToBrushConverter : IValueConverter
{
    private static readonly Dictionary<RuntimeSignal, Color> Palette = new()
    {
        [RuntimeSignal.Running] = Color.FromArgb(255, 63, 185, 80),
        [RuntimeSignal.Healthy] = Color.FromArgb(255, 46, 160, 67),
        [RuntimeSignal.Unhealthy] = Color.FromArgb(255, 229, 83, 75),
        [RuntimeSignal.Starting] = Color.FromArgb(255, 210, 153, 34),
        [RuntimeSignal.Paused] = Color.FromArgb(255, 88, 166, 255),
        [RuntimeSignal.Stopped] = Color.FromArgb(255, 110, 118, 129),
        [RuntimeSignal.Unknown] = Color.FromArgb(255, 110, 118, 129)
    };

    public object Convert(object value, Type targetType, object parameter, string language) =>
        new SolidColorBrush(Palette[value is RuntimeSignal signal ? signal : RuntimeSignal.Unknown]);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
