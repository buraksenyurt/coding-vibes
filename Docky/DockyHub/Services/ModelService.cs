using DockyHub.Models;
using DockyHub.Repositories;
using DockyHub.Data.Entities;
using System.Text.Json;

namespace DockyHub.Services;

public interface IModelService
{
    Task<IEnumerable<ModelCatalogResponse>> GetAllModelsAsync();
    Task<ModelDefinition?> GetModelAsync(string modelName);
    Task<string> GenerateDockerComposeAsync(string modelName, Dictionary<string, object>? options = null);
    Task<ModelDefinition> CreateModelAsync(ModelDefinition modelDefinition);
    Task<ModelDefinition?> UpdateModelAsync(string modelName, ModelDefinition modelDefinition);
    Task<bool> DeleteModelAsync(string modelName);
}

public class ModelService : IModelService
{
    private readonly IModelRepository _modelRepository;
    private readonly IServiceCatalogService _serviceCatalogService;

    public ModelService(IModelRepository modelRepository, IServiceCatalogService serviceCatalogService)
    {
        _modelRepository = modelRepository;
        _serviceCatalogService = serviceCatalogService;
    }

    public async Task<IEnumerable<ModelCatalogResponse>> GetAllModelsAsync()
    {
        var models = await _modelRepository.GetAllAsync();
        
        return models.Select(m => new ModelCatalogResponse
        {
            Name = m.Name,
            Description = m.Description,
            Services = JsonSerializer.Deserialize<string[]>(m.Services) ?? Array.Empty<string>(),
            TotalServices = m.TotalServices,
            Ports = JsonSerializer.Deserialize<string[]>(m.Ports) ?? Array.Empty<string>()
        });
    }

    public async Task<ModelDefinition?> GetModelAsync(string modelName)
    {
        var model = await _modelRepository.GetByNameAsync(modelName);
        
        if (model == null)
            return null;

        return MapToModelDefinition(model);
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

    public async Task<ModelDefinition> CreateModelAsync(ModelDefinition modelDefinition)
    {
        var entity = MapToModelEntity(modelDefinition);
        var createdEntity = await _modelRepository.CreateAsync(entity);
        return MapToModelDefinition(createdEntity);
    }

    public async Task<ModelDefinition?> UpdateModelAsync(string modelName, ModelDefinition modelDefinition)
    {
        var existingModel = await _modelRepository.GetByNameAsync(modelName);
        if (existingModel == null)
            return null;

        // Update fields
        existingModel.Description = modelDefinition.Description;
        existingModel.Services = JsonSerializer.Serialize(modelDefinition.Services);
        existingModel.TotalServices = modelDefinition.TotalServices;
        existingModel.Ports = JsonSerializer.Serialize(modelDefinition.Ports);

        var updatedEntity = await _modelRepository.UpdateAsync(existingModel);
        return MapToModelDefinition(updatedEntity);
    }

    public async Task<bool> DeleteModelAsync(string modelName)
    {
        var model = await _modelRepository.GetByNameAsync(modelName);
        if (model == null)
            return false;

        return await _modelRepository.DeleteAsync(model.Id);
    }

    private ModelDefinition MapToModelDefinition(ModelEntity entity)
    {
        return new ModelDefinition
        {
            Name = entity.Name,
            Description = entity.Description,
            Services = JsonSerializer.Deserialize<string[]>(entity.Services) ?? Array.Empty<string>(),
            TotalServices = entity.TotalServices,
            Ports = JsonSerializer.Deserialize<string[]>(entity.Ports) ?? Array.Empty<string>()
        };
    }

    private ModelEntity MapToModelEntity(ModelDefinition definition)
    {
        return new ModelEntity
        {
            Name = definition.Name,
            Description = definition.Description,
            Services = JsonSerializer.Serialize(definition.Services),
            TotalServices = definition.TotalServices,
            Ports = JsonSerializer.Serialize(definition.Ports)
        };
    }
}
