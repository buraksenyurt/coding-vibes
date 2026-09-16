using DockerCity.Parsing.Dto;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace DockerCity.Parsing.Converters;

public sealed class EnvironmentConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(EnvironmentBlock);

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var entries = new List<EnvEntryDto>();

        if (parser.TryConsume<MappingStart>(out _))
        {
            while (!parser.TryConsume<MappingEnd>(out _))
            {
                var key = parser.Consume<Scalar>().Value;
                var value = parser.Consume<Scalar>().Value;
                entries.Add(new EnvEntryDto(key, string.IsNullOrEmpty(value) ? null : value));
            }

            return new EnvironmentBlock(EnvironmentSyntax.Mapping, entries);
        }

        if (parser.TryConsume<SequenceStart>(out _))
        {
            while (!parser.TryConsume<SequenceEnd>(out _))
            {
                var raw = parser.Consume<Scalar>().Value;
                var separator = raw.IndexOf('=');

                entries.Add(separator < 0
                    ? new EnvEntryDto(raw, null)
                    : new EnvEntryDto(raw[..separator], raw[(separator + 1)..]));
            }

            return new EnvironmentBlock(EnvironmentSyntax.Sequence, entries);
        }

        parser.TryConsume<Scalar>(out _);
        return EnvironmentBlock.Empty;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
        => throw new NotSupportedException("This app only reads Docker compose files.");
}