using DockerCity.Parsing.Compose;
using DockerCity.Parsing.Dto;

namespace DockerCity.Parsing.Tests;

public class ComposeFileReaderTests
{
    private static ComposeFileDto Sample() =>
        new ComposeFileReader().ReadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "docker-compose.yml"));

    [Fact]
    public void All_services_are_read()
    {
        Assert.Equal(10, Sample().Services.Count);
    }

    [Fact]
    public void Service_name_with_dash_is_preserved()
    {
        Assert.True(Sample().Services.ContainsKey("ftp-server"));
    }

    [Fact]
    public void Environment_map_is_read()
    {
        var env = Sample().Services["postgres"].Environment;

        Assert.Equal(EnvironmentSyntax.Mapping, env.Syntax);
        Assert.Equal("johndoe", env.Entries.Single(e => e.Key == "POSTGRES_USER").Value);
    }

    [Fact]
    public void Environment_list_is_read()
    {
        var env = Sample().Services["rabbitmq"].Environment;

        Assert.Equal(EnvironmentSyntax.Sequence, env.Syntax);
        Assert.Equal("guest", env.Entries.Single(e => e.Key == "RABBITMQ_DEFAULT_USER").Value);
    }

    [Fact]
    public void Numeric_environment_value_is_converted_to_string()
    {
        var env = Sample().Services["ftp-server"].Environment;

        Assert.Equal("21000", env.Entries.Single(e => e.Key == "PASV_MIN_PORT").Value);
    }

    [Fact]
    public void Command_exec_is_read()
    {
        var command = Sample().Services["keycloak"].Command;

        Assert.NotNull(command);
        Assert.Equal(CommandForm.Exec, command.Form);
        Assert.Equal(["start-dev"], command.Arguments);
    }

    [Fact]
    public void Command_shell_is_read()

    {
        var command = Sample().Services["minio"].Command;

        Assert.NotNull(command);
        Assert.Equal(CommandForm.Shell, command.Form);
        Assert.Equal("server --console-address \":9001\" /data", command.Raw);
    }

    [Fact]
    public void Port_range_is_preserved()
    {
        var ports = Sample().Services["ftp-server"].Ports;

        Assert.Contains("21:21", ports);
        Assert.Contains("21000-21010:21000-21010", ports);
    }

    [Fact]
    public void Container_name_is_null_if_missing()
    {
        Assert.Null(Sample().Services["qdrant"].ContainerName);
        Assert.Equal("fnp-postgres", Sample().Services["postgres"].ContainerName);
    }

    [Fact]
    public void Services_without_networks_return_empty_list()
    {
        var file = Sample();

        Assert.Empty(file.Services["keycloak"].Networks);
        Assert.Empty(file.Services["minio"].Networks);
        Assert.Equal(["fnp-network"], file.Services["postgres"].Networks);
    }


    [Fact]
    public void Top_level_volumes_are_null()
    {
        var volumes = Sample().Volumes;

        Assert.Equal(4, volumes.Count);
        Assert.True(volumes.ContainsKey("postgres_data"));
        Assert.Null(volumes["postgres_data"]);
    }

    [Fact]
    public void Network_definition_is_read_with_driver()
    {
        Assert.Equal("bridge", Sample().Networks["fnp-network"]!.Driver);
    }

    [Fact]
    public void Restart_policy_is_read_if_specified()
    {
        Assert.Equal("always", Sample().Services["qdrant"].Restart);
        Assert.Null(Sample().Services["redis"].Restart);
    }

    [Fact]
    public void Depends_on_is_read()
    {
        Assert.Equal(["postgres"], Sample().Services["pgadmin"].DependsOn);
    }

    [Fact]
    public void Broken_yaml_throws_error_with_location()
    {
        const string bozuk = "services:\n  web:\n    image: nginx\n   ports:\n";

        var ex = Assert.Throws<ComposeParseException>(
            () => new ComposeFileReader().ReadFromText(bozuk));

        Assert.True(ex.Line > 0);
    }
}