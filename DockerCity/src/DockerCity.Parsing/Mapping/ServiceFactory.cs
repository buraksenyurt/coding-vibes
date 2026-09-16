using DockerCity.Domain;
using DockerCity.Domain.Services;
using DockerCity.Domain.Values;
using DockerCity.Parsing.Dto;

namespace DockerCity.Parsing.Mapping;

public sealed class ServiceFactory
{
    private readonly IServiceCategoryResolver _resolver;

    public ServiceFactory(IServiceCategoryResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolver = resolver;
    }

    public ComposeService Create(string name, ComposeServiceDto dto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.Image))
            throw new InvalidOperationException($"There is no image specified for '{name}'");

        var image = ImageRef.Parse(dto.Image);

        var definition = new ServiceDefinition
        {
            Image = image,
            ContainerName = dto.ContainerName,
            Ports = [.. dto.Ports.Select(PortMapping.Parse)],
            Environment = [.. dto.Environment.Entries.Select(e => new EnvVariable(e.Key, e.Value))],
            Volumes = [.. dto.Volumes.Select(VolumeMount.Parse)],
            NetworkNames = [.. dto.Networks],
            DependsOn = [.. dto.DependsOn],
            Command = MapCommand(dto.Command),
            Restart = MapRestart(dto.Restart)
        };

        return _resolver.Resolve(image) switch
        {
            ServiceCategory.Database => new DatabaseService(name, definition),
            ServiceCategory.Messaging => new MessagingService(name, definition),
            ServiceCategory.Storage => new StorageService(name, definition),
            ServiceCategory.Identity => new IdentityService(name, definition),
            ServiceCategory.Tooling => new ToolingService(name, definition),
            ServiceCategory.Gateway => new GatewayService(name, definition),
            _ => new GenericService(name, definition)
        };
    }

    private static CommandSpec? MapCommand(CommandBlock? block) => block is null
        ? null
        : new CommandSpec(
            block.Form == CommandForm.Exec ? CommandKind.Exec : CommandKind.Shell,
            block.Raw,
            block.Arguments);

    private static RestartPolicy MapRestart(string? raw) => raw switch
    {
        "always" => RestartPolicy.Always,
        "on-failure" => RestartPolicy.OnFailure,
        "unless-stopped" => RestartPolicy.UnlessStopped,
        _ => RestartPolicy.No
    };
}