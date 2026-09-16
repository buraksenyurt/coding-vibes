namespace DockerCity.Parsing.Dto;

public sealed class ComposeFileDto
{
    public string? Name { get; set; }
    public Dictionary<string, ComposeServiceDto> Services { get; set; } = [];
    public Dictionary<string, ComposeNetworkDto?> Networks { get; set; } = [];
    public Dictionary<string, ComposeVolumeDto?> Volumes { get; set; } = [];
}

public sealed class ComposeServiceDto
{
    public string? Image { get; set; }
    public string? ContainerName { get; set; }
    public List<string> Ports { get; set; } = [];
    public EnvironmentBlock Environment { get; set; } = EnvironmentBlock.Empty;
    public List<string> Volumes { get; set; } = [];
    public List<string> Networks { get; set; } = [];
    public List<string> DependsOn { get; set; } = [];
    public CommandBlock? Command { get; set; }
    public string? Restart { get; set; }
}

public sealed class ComposeNetworkDto
{
    public string? Driver { get; set; }
    public bool External { get; set; }
}

public sealed class ComposeVolumeDto
{
    public string? Driver { get; set; }
    public bool External { get; set; }
}