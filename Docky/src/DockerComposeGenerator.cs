using Spectre.Console;
using System.Text;

namespace DockyApp;

public class DockerComposeGenerator
{
    public void Generate(GenerationConfig config)
    {
        var services = new Dictionary<string, string>();

        // Base model services
        if (config.Model == "base" || config.Model == "full")
        {
            services.Add("postgresql", GetPostgreSQLService());
            services.Add("pgadmin", GetPgAdminService());
        }

        // Full model additional services
        if (config.Model == "full")
        {
            services.Add("elasticsearch", GetElasticsearchService());
            services.Add("kibana", GetKibanaService());
            services.Add("redis", GetRedisService(6379));
            services.Add("rabbitmq", GetRabbitMQService());
            services.Add("sonarqube", GetSonarQubeService());
        }

        // Additional services based on flags
        if (config.AddRedis && !services.ContainsKey("redis"))
        {
            services.Add("redis", GetRedisService(config.RedisPort));
        }

        if (config.AddRabbitMQ && !services.ContainsKey("rabbitmq"))
        {
            services.Add("rabbitmq", GetRabbitMQService());
        }

        if (config.AddOpenSearch && !services.ContainsKey("opensearch"))
        {
            services.Add("opensearch", GetOpenSearchService());
        }

        // Add custom service from file if provided
        if (!string.IsNullOrEmpty(config.AdditionalServicePath))
        {
            AddCustomService(services, config.AdditionalServicePath);
        }

        // Generate docker-compose content
        var dockerComposeContent = GenerateDockerComposeContent(services);

        // Write to file
        File.WriteAllText(config.OutputPath, dockerComposeContent);

        // Display summary
        DisplaySummary(services, config.OutputPath);
    }

    private void DisplaySummary(Dictionary<string, string> services, string outputPath)
    {
        var table = new Table();
        table.AddColumn("Service");
        table.AddColumn("Port(s)");
        table.AddColumn("Description");

        var serviceInfo = new Dictionary<string, (string ports, string description)>
        {
            ["postgresql"] = ("5432", "PostgreSQL Database"),
            ["pgadmin"] = ("8080", "PgAdmin Web Interface"),
            ["elasticsearch"] = ("9200, 9300", "Elasticsearch Search Engine"),
            ["kibana"] = ("5601", "Kibana Dashboard"),
            ["redis"] = ("6379", "Redis Cache"),
            ["rabbitmq"] = ("5672, 15672", "RabbitMQ Message Broker"),
            ["sonarqube"] = ("9000", "SonarQube Code Quality"),
            ["opensearch"] = ("9200, 9600", "OpenSearch Search Engine")
        };

        foreach (var service in services.Keys)
        {
            if (serviceInfo.ContainsKey(service))
            {
                var info = serviceInfo[service];
                table.AddRow(service, info.ports, info.description);
            }
            else
            {
                table.AddRow(service, "Custom", "Custom Service");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]📋 Generated Services:[/]");
        AnsiConsole.Write(table);
    }

    private string GenerateDockerComposeContent(Dictionary<string, string> services)
    {
        var sb = new StringBuilder();
        sb.AppendLine("version: '3.8'");
        sb.AppendLine();
        sb.AppendLine("services:");

        foreach (var service in services)
        {
            sb.AppendLine($"  {service.Key}:");
            var serviceLines = service.Value.Split('\n');
            foreach (var line in serviceLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine($"    {line}");
                }
            }
            sb.AppendLine();
        }

        // Add networks and volumes
        sb.AppendLine("networks:");
        sb.AppendLine("  docky-network:");
        sb.AppendLine("    driver: bridge");
        sb.AppendLine();
        sb.AppendLine("volumes:");
        sb.AppendLine("  postgres_data:");
        sb.AppendLine("  pgadmin_data:");
        if (services.ContainsKey("elasticsearch"))
        {
            sb.AppendLine("  elasticsearch_data:");
        }
        if (services.ContainsKey("sonarqube"))
        {
            sb.AppendLine("  sonarqube_data:");
            sb.AppendLine("  sonarqube_logs:");
            sb.AppendLine("  sonarqube_extensions:");
        }
        if (services.ContainsKey("opensearch"))
        {
            sb.AppendLine("  opensearch_data:");
        }

        return sb.ToString();
    }

    private string GetPostgreSQLService()
    {
        return @"image: postgres:15
container_name: docky_postgres
environment:
  POSTGRES_DB: docky_db
  POSTGRES_USER: docky_user
  POSTGRES_PASSWORD: docky_pass
ports:
  - ""5432:5432""
volumes:
  - postgres_data:/var/lib/postgresql/data
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetPgAdminService()
    {
        return @"image: dpage/pgadmin4:latest
container_name: docky_pgadmin
environment:
  PGADMIN_DEFAULT_EMAIL: admin@docky.local
  PGADMIN_DEFAULT_PASSWORD: admin123
ports:
  - ""8080:80""
volumes:
  - pgadmin_data:/var/lib/pgadmin
networks:
  - docky-network
restart: unless-stopped
depends_on:
  - postgresql";
    }

    private string GetElasticsearchService()
    {
        return @"image: docker.elastic.co/elasticsearch/elasticsearch:8.11.0
container_name: docky_elasticsearch
environment:
  - discovery.type=single-node
  - ""ES_JAVA_OPTS=-Xms512m -Xmx512m""
  - xpack.security.enabled=false
ports:
  - ""9200:9200""
  - ""9300:9300""
volumes:
  - elasticsearch_data:/usr/share/elasticsearch/data
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetKibanaService()
    {
        return @"image: docker.elastic.co/kibana/kibana:8.11.0
container_name: docky_kibana
environment:
  - ELASTICSEARCH_HOSTS=http://elasticsearch:9200
ports:
  - ""5601:5601""
networks:
  - docky-network
restart: unless-stopped
depends_on:
  - elasticsearch";
    }

    private string GetRedisService(int port = 6379)
    {
        return $@"image: redis:7-alpine
container_name: docky_redis
ports:
  - ""{port}:6379""
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetRabbitMQService()
    {
        return @"image: rabbitmq:3-management-alpine
container_name: docky_rabbitmq
environment:
  RABBITMQ_DEFAULT_USER: docky_user
  RABBITMQ_DEFAULT_PASS: docky_pass
ports:
  - ""5672:5672""
  - ""15672:15672""
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetSonarQubeService()
    {
        return @"image: sonarqube:community
container_name: docky_sonarqube
environment:
  - SONAR_ES_BOOTSTRAP_CHECKS_DISABLE=true
ports:
  - ""9000:9000""
volumes:
  - sonarqube_data:/opt/sonarqube/data
  - sonarqube_logs:/opt/sonarqube/logs
  - sonarqube_extensions:/opt/sonarqube/extensions
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetOpenSearchService()
    {
        return @"image: opensearchproject/opensearch:latest
container_name: docky_opensearch
environment:
  - discovery.type=single-node
  - ""OPENSEARCH_JAVA_OPTS=-Xms512m -Xmx512m""
  - plugins.security.disabled=true
ports:
  - ""9200:9200""
  - ""9600:9600""
volumes:
  - opensearch_data:/usr/share/opensearch/data
networks:
  - docky-network
restart: unless-stopped";
    }

    private void AddCustomService(Dictionary<string, string> services, string servicePath)
    {
        try
        {
            if (File.Exists(servicePath))
            {
                var customServiceContent = File.ReadAllText(servicePath);
                var serviceName = Path.GetFileNameWithoutExtension(servicePath);
                services.Add(serviceName, customServiceContent);
                AnsiConsole.MarkupLine($"[yellow]📁 Added custom service from: {servicePath}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]⚠️  Custom service file not found: {servicePath}[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Error adding custom service: {ex.Message}[/]");
        }
    }
}
