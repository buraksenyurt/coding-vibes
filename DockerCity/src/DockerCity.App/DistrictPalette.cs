using Windows.UI;

namespace DockerCity.App;

// Eight hues that stay apart from each other on a dark background. Districts
// are coloured by position, not by name, so the same file always looks the
// same but two different files can reuse a colour without confusion.
internal static class DistrictPalette
{
    private static readonly Color[] Hues =
    [
        Color.FromArgb(255, 86, 140, 214),
        Color.FromArgb(255, 214, 132, 68),
        Color.FromArgb(255, 78, 176, 150),
        Color.FromArgb(255, 176, 106, 196),
        Color.FromArgb(255, 122, 172, 92),
        Color.FromArgb(255, 210, 94, 122),
        Color.FromArgb(255, 108, 148, 196),
        Color.FromArgb(255, 196, 176, 84)
    ];

    // The implicit default network is not a place anyone chose, so it stays
    // neutral and is drawn with a dashed border.
    private static readonly Color Neutral = Color.FromArgb(255, 150, 152, 158);

    public static Color For(int index, bool isImplicit) =>
        isImplicit ? Neutral : Hues[index % Hues.Length];
}
