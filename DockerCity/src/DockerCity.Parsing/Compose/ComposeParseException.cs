namespace DockerCity.Parsing.Compose;

public sealed class ComposeParseException(string message, long line, long column, Exception? inner = null)
    : Exception(message, inner)
{
    public long Line { get; } = line;
    public long Column { get; } = column;
}