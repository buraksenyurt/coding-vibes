using DockyHub.Models;
using YamlDotNet.Serialization;

namespace DockyHub.Services;

public interface IModelService
{
    Task<IEnumerable<ModelCatalogResponse>> GetAllModelsAsync();
    Task<ModelDefinition?> GetModelAsync(string modelName);
    Task<string> GenerateDockerComposeAsync(string modelName, Dictionary<string, object>? options = null);
}

public class ModelService : IModelService
{
    private readonly string _modelsPath;
    private readonly IDeserializer _yamlDeserializer;
    private readonly IServiceCatalogService _serviceCatalogService;

    public ModelService(IServiceCatalogService serviceCatalogService)
    {
        _modelsPath = Path.Combine(Directory.GetCurrentDirectory(), "Repository", "Models");
        _yamlDeserializer = new DeserializerBuilder().Build();
        _serviceCatalogService = serviceCatalogService;
    }

    public async Task<IEnumerable<ModelCatalogResponse>> GetAllModelsAsync()
    {
        var models = new List<ModelCatalogResponse>();

        if (!Directory.Exists(_modelsPath))
            return models;

        var modelFiles = Directory.GetFiles(_modelsPath, "*.yml");

        foreach (var file in modelFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                var modelDefinition = _yamlDeserializer.Deserialize<ModelDefinition>(content);

                models.Add(new ModelCatalogResponse
                {
                    Name = modelDefinition.Name,
                    Description = modelDefinition.Description,
                    Services = modelDefinition.Services,
                    TotalServices = modelDefinition.TotalServices,
                    Ports = modelDefinition.Ports
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {file}: {ex.Message}");
            }
        }

        return models.OrderBy(m => m.Name);
    }

    public async Task<ModelDefinition?> GetModelAsync(string modelName)
    {
        var filePath = Path.Combine(_modelsPath, $"{modelName.ToLower()}.yml");

        if (!File.Exists(filePath))
            return null;

        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            return _yamlDeserializer.Deserialize<ModelDefinition>(content);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading model {modelName}: {ex.Message}");
            return null;
        }
    }

    public async Task<string> GenerateDockerComposeAsync(string modelName, Dictionary<string, object>? options = null)
    {
        var model = await GetModelAsync(modelName);
        if (model == null)
            throw new ArgumentException($"Model '{modelName}' not found");

        var compose = new System.Text.StringBuilder();
        compose.AppendLine("version: '3.8'");
        compose.AppendLine();
        compose.AppendLine("services:");

        // Add services from model
        foreach (var serviceName in model.Services)
        {
            var service = await _serviceCatalogService.GetServiceAsync(serviceName);
            if (service != null)
            {
                compose.AppendLine($"  {serviceName}:");
                var serviceLines = service.DockerComposeContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in serviceLines)
                {
                    var trimmedLine = line.TrimEnd('\r');
                    if (!string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        compose.AppendLine($"    {trimmedLine}");
                    }
                }
                compose.AppendLine();
            }
        }

        // Add networks and volumes
        compose.AppendLine("networks:");
        compose.AppendLine("  docky-network:");
        compose.AppendLine("    driver: bridge");
        compose.AppendLine();
        compose.AppendLine("volumes:");
        
        // Add volumes based on services
        var volumeServices = new[] { "postgresql", "pgadmin", "elasticsearch", "grafana", "prometheus" };
        foreach (var serviceName in model.Services)
        {
            if (volumeServices.Contains(serviceName))
            {
                compose.AppendLine($"  {serviceName}_data:");
            }
        }

        return compose.ToString();
    }
}
