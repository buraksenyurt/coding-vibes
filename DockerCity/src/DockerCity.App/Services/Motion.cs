using Windows.UI.ViewManagement;

namespace DockerCity.App.Services;

// Windows has a switch for this: Settings > Accessibility > Visual effects >
// Animation effects. Some people turn it off because movement makes them
// unwell, so every decorative animation in the app asks here first.
internal static class Motion
{
    private static readonly UISettings Settings = new();

    public static bool IsEnabled => Settings.AnimationsEnabled;
}
