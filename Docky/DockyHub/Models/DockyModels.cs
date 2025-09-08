namespace DockyHub.Models;

public class ServiceDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Ports { get; set; } = Array.Empty<string>();
    public string DockerComposeContent { get; set; } = string.Empty;
    public string Version { get; set; } = "latest";
    public Dictionary<string, string> Environment { get; set; } = new();
    public string[] Volumes { get; set; } = Array.Empty<string>();
    public string[] Networks { get; set; } = Array.Empty<string>();
}

public class ModelDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Services { get; set; } = Array.Empty<string>();
    public int TotalServices { get; set; }
    public string[] Ports { get; set; } = Array.Empty<string>();
}

public class ServiceCatalogResponse
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Ports { get; set; } = Array.Empty<string>();
    public string Version { get; set; } = "latest";
}

public class ModelCatalogResponse
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Services { get; set; } = Array.Empty<string>();
    public int TotalServices { get; set; }
    public string[] Ports { get; set; } = Array.Empty<string>();
}
