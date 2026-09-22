using DockerCity.Data.Stores;

namespace DockerCity.Data.Preferences;

// Typed access over the key/value AppSettings table. Every getter has a
// default, so a missing or unreadable value degrades to the out-of-the-box
// behaviour instead of an exception.
public sealed class AppPreferences(IAppSettingsStore store)
{
    public const string ThemeKey = "app.theme";
    public const string WindowKey = "window.placement";
    public const string ReopenLastFileKey = "app.reopen-last-file";
    public const string LastFileKey = "app.last-file";
    public const string ShowDetailsKey = "view.details";
    public const string ShowLinksKey = "view.links";
    public const string ShowDistrictsKey = "view.districts";
    public const string ShowMinimapKey = "view.minimap";
    public const string ShowLiveStatusKey = "view.live";

    public async Task<ThemePreference> GetThemeAsync(CancellationToken cancellationToken = default) =>
        Enum.TryParse<ThemePreference>(await store.GetAsync(ThemeKey, cancellationToken), ignoreCase: true, out var theme)
        && Enum.IsDefined(theme)
            ? theme
            : ThemePreference.System;

    public Task SetThemeAsync(ThemePreference theme, CancellationToken cancellationToken = default) =>
        store.SetAsync(ThemeKey, theme.ToString(), cancellationToken);

    public async Task<WindowPlacement?> GetWindowAsync(CancellationToken cancellationToken = default) =>
        WindowPlacement.TryParse(await store.GetAsync(WindowKey, cancellationToken));

    public Task SetWindowAsync(WindowPlacement placement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(placement);
        return store.SetAsync(WindowKey, placement.ToString(), cancellationToken);
    }

    public Task<bool> GetReopenLastFileAsync(CancellationToken cancellationToken = default) =>
        GetBoolAsync(ReopenLastFileKey, fallback: false, cancellationToken);

    public Task SetReopenLastFileAsync(bool value, CancellationToken cancellationToken = default) =>
        SetBoolAsync(ReopenLastFileKey, value, cancellationToken);

    public Task<string?> GetLastFileAsync(CancellationToken cancellationToken = default) =>
        store.GetAsync(LastFileKey, cancellationToken);

    public Task SetLastFileAsync(string? path, CancellationToken cancellationToken = default) =>
        store.SetAsync(LastFileKey, path, cancellationToken);

    // Layers are on unless the user turned them off.
    public Task<bool> GetLayerVisibleAsync(string key, CancellationToken cancellationToken = default) =>
        GetBoolAsync(key, fallback: true, cancellationToken);

    public Task SetLayerVisibleAsync(string key, bool visible, CancellationToken cancellationToken = default) =>
        SetBoolAsync(key, visible, cancellationToken);

    private async Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken cancellationToken) =>
        bool.TryParse(await store.GetAsync(key, cancellationToken), out var value) ? value : fallback;

    private Task SetBoolAsync(string key, bool value, CancellationToken cancellationToken) =>
        store.SetAsync(key, value ? "true" : "false", cancellationToken);
}
