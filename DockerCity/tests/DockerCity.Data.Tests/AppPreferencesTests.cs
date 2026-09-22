using DockerCity.Data.Preferences;
using DockerCity.Data.Stores;

namespace DockerCity.Data.Tests;

public class AppPreferencesTests
{
    private static AppPreferences PreferencesFor(TemporaryDatabase database) =>
        new(new AppSettingsStore(database.Context));

    [Fact]
    public async Task Theme_defaults_to_system_and_round_trips()
    {
        using var database = new TemporaryDatabase();
        var preferences = PreferencesFor(database);

        Assert.Equal(ThemePreference.System, await preferences.GetThemeAsync());

        await preferences.SetThemeAsync(ThemePreference.Dark);
        Assert.Equal(ThemePreference.Dark, await preferences.GetThemeAsync());
    }

    [Theory]
    [InlineData("purple")]
    [InlineData("42")]
    [InlineData("")]
    public async Task An_unreadable_theme_falls_back_to_system(string stored)
    {
        using var database = new TemporaryDatabase();
        var preferences = PreferencesFor(database);

        await new AppSettingsStore(database.Context).SetAsync(AppPreferences.ThemeKey, stored);

        Assert.Equal(ThemePreference.System, await preferences.GetThemeAsync());
    }

    [Fact]
    public async Task Window_placement_round_trips()
    {
        using var database = new TemporaryDatabase();
        var preferences = PreferencesFor(database);

        var placement = new WindowPlacement(-1200, 80, 1440, 900, IsMaximized: true);
        await preferences.SetWindowAsync(placement);

        Assert.Equal(placement, await preferences.GetWindowAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("10,20,800")]
    [InlineData("a,b,c,d,0")]
    [InlineData("0,0,100,100,0")]
    public void Malformed_or_tiny_placements_are_ignored(string? stored)
    {
        Assert.Null(WindowPlacement.TryParse(stored));
    }

    [Fact]
    public async Task Layers_are_visible_until_turned_off()
    {
        using var database = new TemporaryDatabase();
        var preferences = PreferencesFor(database);

        Assert.True(await preferences.GetLayerVisibleAsync(AppPreferences.ShowLinksKey));

        await preferences.SetLayerVisibleAsync(AppPreferences.ShowLinksKey, false);

        Assert.False(await preferences.GetLayerVisibleAsync(AppPreferences.ShowLinksKey));
        Assert.True(await preferences.GetLayerVisibleAsync(AppPreferences.ShowDistrictsKey));
    }

    [Fact]
    public async Task Reopening_the_last_file_is_off_by_default()
    {
        using var database = new TemporaryDatabase();
        var preferences = PreferencesFor(database);

        Assert.False(await preferences.GetReopenLastFileAsync());
        Assert.Null(await preferences.GetLastFileAsync());

        await preferences.SetReopenLastFileAsync(true);
        await preferences.SetLastFileAsync(@"C:\work\shop\docker-compose.yml");

        Assert.True(await preferences.GetReopenLastFileAsync());
        Assert.Equal(@"C:\work\shop\docker-compose.yml", await preferences.GetLastFileAsync());
    }
}
