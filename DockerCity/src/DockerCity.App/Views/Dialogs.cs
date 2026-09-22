using System.Reflection;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockerCity.App.Views;

// Built in code rather than XAML: both dialogs are a few lines of text, and a
// ContentDialog needs its XamlRoot set from the window that shows it anyway.
internal static class Dialogs
{
    private const string RepositoryUrl = "https://github.com/buraksenyurt/coding-vibes/tree/main/DockerCity";

    public static readonly IReadOnlyList<(string Keys, string Action)> Shortcuts =
    [
        ("Ctrl+O", "Open a compose file"),
        ("F5", "Reload the open file"),
        ("Ctrl+E", "Export the city as PNG"),
        ("Ctrl+R", "Reset the layout"),
        ("Ctrl+1", "Show or hide the details panel"),
        ("Ctrl+2", "Show or hide roads"),
        ("Ctrl+3", "Show or hide district borders"),
        ("Ctrl+4", "Show or hide the mini map"),
        ("Ctrl+Plus / Ctrl+Minus", "Zoom in or out (the numeric keypad works too)"),
        ("Ctrl+Mouse wheel", "Zoom around the pointer"),
        ("Ctrl+0", "Actual size (100%)"),
        ("Ctrl+9", "Fit the city to the window"),
        ("Drag empty ground", "Pan the city"),
        ("Esc", "Clear the selection"),
        ("F1", "Keyboard shortcuts"),
        ("Drag a .yml file", "Open it by dropping it on the window")
    ];

    public static string Version
    {
        get
        {
            var informational = typeof(Dialogs).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            // Source Link appends "+<commit>"; the commit is noise here.
            return informational?.Split('+')[0] ?? "0.0.0";
        }
    }

    public static Task ShowAboutAsync(XamlRoot root, int mappingCount, string databaseFile)
    {
        var content = new StackPanel { Spacing = 10, MaxWidth = 440 };

        content.Children.Add(Text($"Version {Version}", opacity: 0.6));

        content.Children.Add(Text(
            "DockerCity reads a docker-compose file and draws it as a small city: services are the residents, "
            + "networks are the districts and depends_on relations are the roads between them."));

        content.Children.Add(Text(
            "It was built phase by phase as a learning workshop. Every feature was written up as a tutorial "
            + "before it was coded, and the documents in docs/ record why each decision was made, "
            + "including the ones that turned out to be wrong."));

        content.Children.Add(Text(
            ".NET 10 · WinUI 3 · YamlDotNet · EF Core + SQLite · CommunityToolkit.Mvvm",
            opacity: 0.7));

        content.Children.Add(Text($"Image catalog: {mappingCount} mappings", opacity: 0.7, size: 12));
        content.Children.Add(Text($"Database: {databaseFile}", opacity: 0.7, size: 12));

        content.Children.Add(new HyperlinkButton
        {
            Content = "Source and tutorials on GitHub",
            NavigateUri = new Uri(RepositoryUrl),
            Padding = new Thickness(0)
        });

        content.Children.Add(Text(
            "Made by Burak Selim Şenyurt, pair-programmed with Claude.",
            opacity: 0.6,
            size: 12));

        return ShowAsync(root, "About DockerCity", content);
    }

    public static Task ShowShortcutsAsync(XamlRoot root)
    {
        var grid = new Grid { ColumnSpacing = 24, RowSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (var row = 0; row < Shortcuts.Count; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var keys = Text(Shortcuts[row].Keys);
            keys.FontWeight = FontWeights.SemiBold;
            Grid.SetRow(keys, row);

            var action = Text(Shortcuts[row].Action, opacity: 0.8);
            Grid.SetRow(action, row);
            Grid.SetColumn(action, 1);

            grid.Children.Add(keys);
            grid.Children.Add(action);
        }

        return ShowAsync(root, "Keyboard shortcuts", grid);
    }

    private static async Task ShowAsync(XamlRoot root, string title, UIElement content)
    {
        ArgumentNullException.ThrowIfNull(root);

        var dialog = new ContentDialog
        {
            // Without XamlRoot a ContentDialog in WinUI desktop has nowhere to
            // appear and ShowAsync throws.
            XamlRoot = root,
            Title = title,
            Content = content,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowAsync();
    }

    private static TextBlock Text(string text, double opacity = 1, double size = 14) => new()
    {
        Text = text,
        Opacity = opacity,
        FontSize = size,
        TextWrapping = TextWrapping.Wrap
    };
}
