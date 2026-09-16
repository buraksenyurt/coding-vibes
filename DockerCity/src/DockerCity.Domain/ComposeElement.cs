namespace DockerCity.Domain;

public abstract class ComposeElement
{
    protected ComposeElement(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public string Name { get; }
    public abstract string Describe();
}