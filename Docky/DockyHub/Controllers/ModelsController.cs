using DockyHub.Models;
using DockyHub.Services;
using Microsoft.AspNetCore.Mvc;

namespace DockyHub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModelsController : ControllerBase
{
    private readonly IModelService _modelService;

    public ModelsController(IModelService modelService)
    {
        _modelService = modelService;
    }

    /// <summary>
    /// Get all available models
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ModelCatalogResponse>>> GetAllModels()
    {
        var models = await _modelService.GetAllModelsAsync();
        return Ok(models);
    }

    /// <summary>
    /// Get specific model details
    /// </summary>
    [HttpGet("{modelName}")]
    public async Task<ActionResult<ModelDefinition>> GetModel(string modelName)
    {
        var model = await _modelService.GetModelAsync(modelName);
        
        if (model == null)
            return NotFound($"Model '{modelName}' not found");

        return Ok(model);
    }

    /// <summary>
    /// Generate docker-compose content for a model
    /// </summary>
    [HttpPost("{modelName}/generate")]
    public async Task<ActionResult<string>> GenerateDockerCompose(
        string modelName, 
        [FromBody] Dictionary<string, object>? options = null)
    {
        try
        {
            var dockerCompose = await _modelService.GenerateDockerComposeAsync(modelName, options);
            return Ok(dockerCompose);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error generating docker-compose: {ex.Message}");
        }
    }
}
