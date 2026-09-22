using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace DockerCity.App.Converters;

// Classic {Binding} does not turn a bool into Visibility on its own (x:Bind
// does). Pass "invert" as the parameter to show when false.
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var visible = value is true;

        if (parameter is string text && text.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
