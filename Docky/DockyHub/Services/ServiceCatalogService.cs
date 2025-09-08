using DockyHub.Models;
using YamlDotNet.Serialization;

namespace DockyHub.Services;

public interface IServiceCatalogService
{
    Task<IEnumerable<ServiceCatalogResponse>> GetAllServicesAsync();
    Task<ServiceDefinition?> GetServiceAsync(string serviceName);
    Task<ServiceDefinition?> GetServiceWithPortAsync(string serviceName, int port);
}

public class ServiceCatalogService : IServiceCatalogService
{
    private readonly string _servicesPath;
    private readonly IDeserializer _yamlDeserializer;

    public ServiceCatalogService()
    {
        _servicesPath = Path.Combine(Directory.GetCurrentDirectory(), "Repository", "Services");
        _yamlDeserializer = new DeserializerBuilder().Build();
    }

    public async Task<IEnumerable<ServiceCatalogResponse>> GetAllServicesAsync()
    {
        var services = new List<ServiceCatalogResponse>();

        if (!Directory.Exists(_servicesPath))
            return services;

        var serviceFiles = Directory.GetFiles(_servicesPath, "*.yml");

        foreach (var file in serviceFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                var serviceDefinition = _yamlDeserializer.Deserialize<ServiceDefinition>(content);

                services.Add(new ServiceCatalogResponse
                {
                    Name = serviceDefinition.Name,
                    Description = serviceDefinition.Description,
                    Ports = serviceDefinition.Ports,
                    Version = serviceDefinition.Version
                });
            }
            catch (Exception ex)
            {
                // Log error but continue processing other files
                Console.WriteLine($"Error processing {file}: {ex.Message}");
            }
        }

        return services.OrderBy(s => s.Name);
    }

    public async Task<ServiceDefinition?> GetServiceAsync(string serviceName)
    {
        var filePath = Path.Combine(_servicesPath, $"{serviceName.ToLower()}.yml");

        if (!File.Exists(filePath))
            return null;

        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            return _yamlDeserializer.Deserialize<ServiceDefinition>(content);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading service {serviceName}: {ex.Message}");
            return null;
        }
    }

    public async Task<ServiceDefinition?> GetServiceWithPortAsync(string serviceName, int port)
    {
        var service = await GetServiceAsync(serviceName);
        
        if (service == null)
            return null;

        // Create a copy with custom port configuration
        var customService = new ServiceDefinition
        {
            Name = service.Name,
            Description = service.Description,
            Ports = service.Ports,
            DockerComposeContent = service.DockerComposeContent,
            Version = service.Version,
            Environment = service.Environment,
            Volumes = service.Volumes,
            Networks = service.Networks
        };

        // Replace default port with custom port in docker-compose content
        if (service.Ports.Length > 0)
        {
            var originalPort = service.Ports[0];
            customService.DockerComposeContent = service.DockerComposeContent
                .Replace($"\"{originalPort}:{originalPort}\"", $"\"{port}:{originalPort}\"");
        }

        return customService;
    }
}
