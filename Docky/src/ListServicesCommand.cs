using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace DockyApp;

public class ListServicesCommand : AsyncCommand<ListServicesCommand.Settings>
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
        AnsiConsole.MarkupLine("[bold green]🐳 Available Services[/]");
        AnsiConsole.WriteLine();

        try
        {
            var services = await DockyHubClient.TryGetAsync<ServiceInfo>(settings.HubUrl, "api/services")
                ?? BuiltInCatalog.ToServiceInfos();

            if (services == null || !services.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No services found[/]");
                return 0;
            }

            var table = new Table();
            table.AddColumn("Service");
            table.AddColumn("Description");
            table.AddColumn("Ports");
            
            if (settings.Detailed)
            {
                table.AddColumn("Version");
            }

            foreach (var service in services)
            {
                var portsText = string.Join(", ", service.Ports);
                
                if (settings.Detailed)
                {
                    table.AddRow(service.Name, service.Description, portsText, service.Version);
                }
                else
                {
                    table.AddRow(service.Name, service.Description, portsText);
                }
            }

            AnsiConsole.Write(table);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]💡 Use --add-{service-name} flag with generate command to add these services[/]");
            
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Error: {ex.Message}[/]");
            return 1;
        }
    }
}

public class ServiceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Ports { get; set; } = Array.Empty<string>();
    public string Version { get; set; } = "latest";
}
