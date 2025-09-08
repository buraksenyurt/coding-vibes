using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace DockyApp;

public class GenerateCommand : Command<GenerateCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<type>")]
        [Description("Type of file to generate (docker-compose)")]
        public string Type { get; set; } = "docker-compose";

        [CommandOption("--model")]
        [Description("Model type: base or full")]
        [DefaultValue("base")]
        public string Model { get; set; } = "base";

        [CommandOption("--add-redis")]
        [Description("Add Redis service")]
        public bool AddRedis { get; set; }

        [CommandOption("--add-rabbitmq")]
        [Description("Add RabbitMQ service")]
        public bool AddRabbitMQ { get; set; }

        [CommandOption("--add-opensearch")]
        [Description("Add OpenSearch service")]
        public bool AddOpenSearch { get; set; }

        [CommandOption("--redis-port")]
        [Description("Redis port (default: 6379)")]
        [DefaultValue(6379)]
        public int RedisPort { get; set; } = 6379;

        [CommandOption("--add-service")]
        [Description("Path to additional service YAML file")]
        public string? AdditionalServicePath { get; set; }

        [CommandOption("--output")]
        [Description("Output file path")]
        [DefaultValue("docker-compose.yml")]
        public string OutputPath { get; set; } = "docker-compose.yml";
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        AnsiConsole.MarkupLine("[bold green]🐳 Docky CLI Tool[/]");
        AnsiConsole.WriteLine();

        if (settings.Type.ToLower() != "docker-compose")
        {
            AnsiConsole.MarkupLine("[red]Error: Only 'docker-compose' type is supported[/]");
            return 1;
        }

        var generator = new DockerComposeGenerator();
        
        try
        {
            var config = new GenerationConfig
            {
                Model = settings.Model.ToLower(),
                AddRedis = settings.AddRedis,
                AddRabbitMQ = settings.AddRabbitMQ,
                AddOpenSearch = settings.AddOpenSearch,
                RedisPort = settings.RedisPort,
                AdditionalServicePath = settings.AdditionalServicePath,
                OutputPath = settings.OutputPath
            };

            generator.Generate(config);
            
            AnsiConsole.MarkupLine($"[green]✅ Docker Compose file generated successfully at: {settings.OutputPath}[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Error: {ex.Message}[/]");
            return 1;
        }
    }
}
