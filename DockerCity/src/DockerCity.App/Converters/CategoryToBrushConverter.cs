using DockerCity.Domain;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DockerCity.App.Converters;

public sealed class CategoryToBrushConverter : IValueConverter
{
    private static readonly Color Fallback = Color.FromArgb(255, 108, 117, 125);

    private static readonly Dictionary<ServiceCategory, Color> Palette = new()
    {
        [ServiceCategory.Database] = Color.FromArgb(255, 54, 110, 180),
        [ServiceCategory.Messaging] = Color.FromArgb(255, 176, 96, 40),
        [ServiceCategory.Storage] = Color.FromArgb(255, 40, 135, 120),
        [ServiceCategory.Identity] = Color.FromArgb(255, 132, 70, 160),
        [ServiceCategory.Tooling] = Color.FromArgb(255, 70, 130, 70),
        [ServiceCategory.Gateway] = Color.FromArgb(255, 170, 60, 90),
        [ServiceCategory.Unknown] = Fallback
    };

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var category = value is ServiceCategory known ? known : ServiceCategory.Unknown;

        return new SolidColorBrush(Palette.TryGetValue(category, out var color) ? color : Fallback);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
