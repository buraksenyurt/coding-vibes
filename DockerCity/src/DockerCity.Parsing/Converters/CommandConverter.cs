using DockerCity.Parsing.Dto;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace DockerCity.Parsing.Converters;

public sealed class CommandConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(CommandBlock);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (parser.TryConsume<SequenceStart>(out _))
        {
            var args = new List<string>();

            while (!parser.TryConsume<SequenceEnd>(out _))
                args.Add(parser.Consume<Scalar>().Value);

            return new CommandBlock(CommandForm.Exec, null, args);
        }

        var scalar = parser.Consume<Scalar>();

        return string.IsNullOrWhiteSpace(scalar.Value)
            ? null
            : new CommandBlock(CommandForm.Shell, scalar.Value, []);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
        => throw new NotSupportedException("This app only reads Docker compose files.");
}