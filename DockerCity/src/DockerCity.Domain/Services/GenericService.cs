namespace DockerCity.Domain.Services;

// Fallback for images the resolver does not recognise. Keeps the map complete
// instead of dropping services we cannot classify.
public sealed class GenericService : ComposeService
{
    public GenericService(string name, ServiceDefinition definition)
        : base(name, definition) { }

    public override ServiceCategory Category => ServiceCategory.Unknown;
}
