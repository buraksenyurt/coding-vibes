using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.Json;

namespace DockyApp;

public class ListModelsCommand : AsyncCommand<ListModelsCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--hub-url")]
        [Description("DockyHub API URL")]
        [DefaultValue("http://localhost:5000")]
        public string HubUrl { get; set; } = "http://localhost:5000";

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
            using var httpClient = new HttpClient();
            var response = await httpClient.GetStringAsync($"{settings.HubUrl}/api/models");
            var models = JsonSerializer.Deserialize<ModelInfo[]>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

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
