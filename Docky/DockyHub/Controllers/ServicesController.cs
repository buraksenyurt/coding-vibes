using DockyHub.Models;
using DockyHub.Services;
using Microsoft.AspNetCore.Mvc;

namespace DockyHub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly IServiceCatalogService _serviceCatalogService;

    public ServicesController(IServiceCatalogService serviceCatalogService)
    {
        _serviceCatalogService = serviceCatalogService;
    }

    /// <summary>
    /// Get all available services
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServiceCatalogResponse>>> GetAllServices()
    {
        var services = await _serviceCatalogService.GetAllServicesAsync();
        return Ok(services);
    }

    /// <summary>
    /// Get specific service details
    /// </summary>
    [HttpGet("{serviceName}")]
    public async Task<ActionResult<ServiceDefinition>> GetService(string serviceName)
    {
        var service = await _serviceCatalogService.GetServiceAsync(serviceName);
        
        if (service == null)
            return NotFound($"Service '{serviceName}' not found");

        return Ok(service);
    }

    /// <summary>
    /// Get service with custom port configuration
    /// </summary>
    [HttpGet("{serviceName}/port/{port}")]
    public async Task<ActionResult<ServiceDefinition>> GetServiceWithPort(string serviceName, int port)
    {
        var service = await _serviceCatalogService.GetServiceWithPortAsync(serviceName, port);
        
        if (service == null)
            return NotFound($"Service '{serviceName}' not found");

        return Ok(service);
    }

    /// <summary>
    /// Create a new service
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ServiceDefinition>> CreateService([FromBody] ServiceDefinition serviceDefinition)
    {
        try
        {
            var createdService = await _serviceCatalogService.CreateServiceAsync(serviceDefinition);
            return CreatedAtAction(nameof(GetService), new { serviceName = createdService.Name }, createdService);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error creating service: {ex.Message}");
        }
    }

    /// <summary>
    /// Update an existing service
    /// </summary>
    [HttpPut("{serviceName}")]
    public async Task<ActionResult<ServiceDefinition>> UpdateService(string serviceName, [FromBody] ServiceDefinition serviceDefinition)
    {
        var updatedService = await _serviceCatalogService.UpdateServiceAsync(serviceName, serviceDefinition);
        
        if (updatedService == null)
            return NotFound($"Service '{serviceName}' not found");

        return Ok(updatedService);
    }

    /// <summary>
    /// Delete a service
    /// </summary>
    [HttpDelete("{serviceName}")]
    public async Task<ActionResult> DeleteService(string serviceName)
    {
        var deleted = await _serviceCatalogService.DeleteServiceAsync(serviceName);
        
        if (!deleted)
            return NotFound($"Service '{serviceName}' not found");

        return NoContent();
    }
}
