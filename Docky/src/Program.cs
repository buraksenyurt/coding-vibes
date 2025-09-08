using Spectre.Console;
using Spectre.Console.Cli;

namespace DockyApp;

public class Program
{
    public static int Main(string[] args)
    {
        var app = new CommandApp();
        
        app.Configure(config =>
        {
            config.AddCommand<GenerateCommand>("generate")
                .WithDescription("Generate docker-compose files")
                .WithExample(new[] { "generate", "docker-compose", "--model", "base" })
                .WithExample(new[] { "generate", "docker-compose", "--model", "full" })
                .WithExample(new[] { "generate", "docker-compose", "--model", "microservices" })
                .WithExample(new[] { "generate", "docker-compose", "--model", "ai-ml" })
                .WithExample(new[] { "generate", "docker-compose", "--model", "analytics" })
                .WithExample(new[] { "generate", "docker-compose", "--model", "base", "--add-redis" });

            config.AddCommand<ListModelsCommand>("list-models")
                .WithDescription("List all available models")
                .WithExample(new[] { "list-models" })
                .WithExample(new[] { "list-models", "--detailed" });

            config.AddCommand<ListServicesCommand>("list-services")
                .WithDescription("List all available services")
                .WithExample(new[] { "list-services" })
                .WithExample(new[] { "list-services", "--detailed" });
        });

        return app.Run(args);
    }
}
