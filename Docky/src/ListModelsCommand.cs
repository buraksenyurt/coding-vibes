using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace DockyApp;

public class ListModelsCommand : AsyncCommand<ListModelsCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--hub-url")]
        [Description("DockyHub API URL")]
        [DefaultValue(DockyHubClient.DefaultUrl)]
        public string HubUrl { get; set; } = DockyHubClient.DefaultUrl;

        [CommandOption("--detailed")]
        [Description("Show detailed information")]
        public bool Detailed { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        AnsiConsole.MarkupLine("[bold green]🐳 Available Models[/]");
        AnsiConsole.WriteLine();

        try
        {
            var models = await DockyHubClient.TryGetAsync<ModelInfo>(settings.HubUrl, "api/models")
                ?? BuiltInCatalog.ToModelInfos();

            if (models == null || !models.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No models found[/]");
                return 0;
            }

            var table = new Table();
            table.AddColumn("Model");
            table.AddColumn("Description");
            table.AddColumn("Services");
            
            if (settings.Detailed)
            {
                table.AddColumn("Ports");
            }

            foreach (var model in models)
            {
                var servicesText = string.Join(", ", model.Services);
                
                if (settings.Detailed)
                {
                    var portsText = string.Join(", ", model.Ports);
                    table.AddRow(model.Name, model.Description, $"{model.TotalServices} services", portsText);
                }
                else
                {
                    table.AddRow(model.Name, model.Description, $"{model.TotalServices} services");
                }
            }

            AnsiConsole.Write(table);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Error: {ex.Message}[/]");
            return 1;
        }
    }
}

public class ModelInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Services { get; set; } = Array.Empty<string>();
    public int TotalServices { get; set; }
    public string[] Ports { get; set; } = Array.Empty<string>();
}
