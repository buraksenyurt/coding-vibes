using System.Globalization;

namespace DockerCity.Data.Preferences;

// Stored as "x,y,width,height,maximized". Parsing is forgiving on purpose: a
// hand-edited or truncated value must fall back to defaults, never crash the
// window on startup.
public sealed record WindowPlacement(int X, int Y, int Width, int Height, bool IsMaximized)
{
    public const int MinimumWidth = 640;
    public const int MinimumHeight = 480;

    public override string ToString() => string.Join(
        ',',
        X.ToString(CultureInfo.InvariantCulture),
        Y.ToString(CultureInfo.InvariantCulture),
        Width.ToString(CultureInfo.InvariantCulture),
        Height.ToString(CultureInfo.InvariantCulture),
        IsMaximized ? "1" : "0");

    public static WindowPlacement? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var parts = text.Split(',');

        if (parts.Length != 5)
        {
            return null;
        }

        var numbers = new int[4];

        for (var index = 0; index < 4; index++)
        {
            if (!int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[index]))
            {
                return null;
            }
        }

        if (numbers[2] < MinimumWidth || numbers[3] < MinimumHeight)
        {
            return null;
        }

        return new WindowPlacement(numbers[0], numbers[1], numbers[2], numbers[3], parts[4] == "1");
    }
}
