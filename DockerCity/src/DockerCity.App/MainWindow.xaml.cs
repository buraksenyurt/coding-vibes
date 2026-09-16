using DockerCity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;

namespace DockerCity.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "DockerCity";

        _ = ShowDatabaseStatusAsync();
    }

    // Phase 3 has no UI of its own yet. Reporting the store here is enough to
    // prove the database is created and seeded on first run.
    private async Task ShowDatabaseStatusAsync()
    {
        try
        {
            using var context = await DatabaseInitializer.OpenAsync();
            var mappings = await context.ImageMappings.CountAsync();

            DatabaseStatusText.Text =
                $"{mappings} image mappings ready · {DockerCityPaths.DatabaseFile}";
        }
        catch (Exception exception)
        {
            DatabaseStatusText.Text = $"Database unavailable: {exception.Message}";
        }
    }
}
