namespace DockerCity.Parsing.Dto;

public enum EnvironmentSyntax
{
    None,
    Mapping,
    Sequence
}

public sealed record EnvEntryDto(string Key, string? Value);

public sealed record EnvironmentBlock(
    EnvironmentSyntax Syntax,
    IReadOnlyList<EnvEntryDto> Entries
)
{
    public static readonly EnvironmentBlock Empty = new(EnvironmentSyntax.None, []);
}

public enum CommandForm
{
    Shell,
    Exec
}

public sealed record CommandBlock(CommandForm Form, string? Raw, IReadOnlyList<string> Arguments);
