using DockyHub.Models;
using DockyHub.Repositories;
using DockyHub.Data.Entities;
using System.Text.Json;

namespace DockyHub.Services;

public interface IServiceCatalogService
{
    Task<IEnumerable<ServiceCatalogResponse>> GetAllServicesAsync();
    Task<ServiceDefinition?> GetServiceAsync(string serviceName);
    Task<ServiceDefinition?> GetServiceWithPortAsync(string serviceName, int port);
    Task<ServiceDefinition> CreateServiceAsync(ServiceDefinition serviceDefinition);
    Task<ServiceDefinition?> UpdateServiceAsync(string serviceName, ServiceDefinition serviceDefinition);
    Task<bool> DeleteServiceAsync(string serviceName);
}

public class ServiceCatalogService : IServiceCatalogService
{
    private readonly IServiceRepository _serviceRepository;

    public ServiceCatalogService(IServiceRepository serviceRepository)
    {
        _serviceRepository = serviceRepository;
    }

    public async Task<IEnumerable<ServiceCatalogResponse>> GetAllServicesAsync()
    {
        var services = await _serviceRepository.GetAllAsync();
        
        return services.Select(s => new ServiceCatalogResponse
        {
            Name = s.Name,
            Description = s.Description,
            Ports = JsonSerializer.Deserialize<string[]>(s.Ports) ?? Array.Empty<string>(),
            Version = s.Version
        });
    }

    public async Task<ServiceDefinition?> GetServiceAsync(string serviceName)
    {
        var service = await _serviceRepository.GetByNameAsync(serviceName);
        
        if (service == null)
            return null;

        return MapToServiceDefinition(service);
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

    public async Task<ServiceDefinition> CreateServiceAsync(ServiceDefinition serviceDefinition)
    {
        var entity = MapToServiceEntity(serviceDefinition);
        var createdEntity = await _serviceRepository.CreateAsync(entity);
        return MapToServiceDefinition(createdEntity);
    }

    public async Task<ServiceDefinition?> UpdateServiceAsync(string serviceName, ServiceDefinition serviceDefinition)
    {
        var existingService = await _serviceRepository.GetByNameAsync(serviceName);
        if (existingService == null)
            return null;

        // Update fields
        existingService.Description = serviceDefinition.Description;
        existingService.Ports = JsonSerializer.Serialize(serviceDefinition.Ports);
        existingService.DockerComposeContent = serviceDefinition.DockerComposeContent;
        existingService.Version = serviceDefinition.Version;
        existingService.Environment = JsonSerializer.Serialize(serviceDefinition.Environment);
        existingService.Volumes = JsonSerializer.Serialize(serviceDefinition.Volumes);
        existingService.Networks = JsonSerializer.Serialize(serviceDefinition.Networks);

        var updatedEntity = await _serviceRepository.UpdateAsync(existingService);
        return MapToServiceDefinition(updatedEntity);
    }

    public async Task<bool> DeleteServiceAsync(string serviceName)
    {
        var service = await _serviceRepository.GetByNameAsync(serviceName);
        if (service == null)
            return false;

        return await _serviceRepository.DeleteAsync(service.Id);
    }

    private ServiceDefinition MapToServiceDefinition(ServiceEntity entity)
    {
        return new ServiceDefinition
        {
            Name = entity.Name,
            Description = entity.Description,
            Ports = JsonSerializer.Deserialize<string[]>(entity.Ports) ?? Array.Empty<string>(),
            DockerComposeContent = entity.DockerComposeContent,
            Version = entity.Version,
            Environment = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.Environment) ?? new Dictionary<string, string>(),
            Volumes = JsonSerializer.Deserialize<string[]>(entity.Volumes) ?? Array.Empty<string>(),
            Networks = JsonSerializer.Deserialize<string[]>(entity.Networks) ?? Array.Empty<string>()
        };
    }

    private ServiceEntity MapToServiceEntity(ServiceDefinition definition)
    {
        return new ServiceEntity
        {
            Name = definition.Name,
            Description = definition.Description,
            Ports = JsonSerializer.Serialize(definition.Ports),
            DockerComposeContent = definition.DockerComposeContent,
            Version = definition.Version,
            Environment = JsonSerializer.Serialize(definition.Environment),
            Volumes = JsonSerializer.Serialize(definition.Volumes),
            Networks = JsonSerializer.Serialize(definition.Networks)
        };
    }
}
