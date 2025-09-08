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
}
