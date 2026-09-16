using DockerCity.Parsing.Converters;
using DockerCity.Parsing.Dto;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DockerCity.Parsing.Compose;

public sealed class ComposeFileReader
{
    private readonly IDeserializer _deserializer;

    public ComposeFileReader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithTypeConverter(new EnvironmentConverter())
            .WithTypeConverter(new CommandConverter())
            .Build();
    }

    public ComposeFileDto ReadFromText(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        try
        {
            return _deserializer.Deserialize<ComposeFileDto>(yaml) ?? new ComposeFileDto();
        }
        catch (YamlException ex)
        {
            throw new ComposeParseException(
                $"Compose file {ex.Start.Line}. line, {ex.Start.Column}. column could not be read.",
                ex.Start.Line,
                ex.Start.Column,
                ex);
        }
    }

    public ComposeFileDto ReadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
            throw new FileNotFoundException("Compose file not found.", path);

        return ReadFromText(File.ReadAllText(path));
    }
}