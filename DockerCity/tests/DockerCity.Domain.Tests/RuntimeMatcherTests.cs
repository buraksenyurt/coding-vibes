using DockerCity.Domain.Runtime;
using DockerCity.Domain.Services;
using DockerCity.Domain.Values;

namespace DockerCity.Domain.Tests;

public class RuntimeMatcherTests
{
    private const string ComposeFile = @"C:\work\dockercity\docker-compose.yml";

    private static ComposeService Service(string name, string? containerName = null) =>
        new GenericService(name, new ServiceDefinition
        {
            Image = ImageRef.Parse("redis:latest"),
            ContainerName = containerName
        });

    private static ContainerSnapshot Container(
        string name,
        string? service = null,
        string? project = null,
        string? configFiles = null,
        ContainerState state = ContainerState.Running) =>
        new(name, name)
        {
            ServiceName = service,
            ProjectName = project,
            ConfigFiles = configFiles,
            State = state
        };

    [Fact]
    public void The_config_file_label_wins()
    {
        // Two projects, both with a service called redis. Only one of them
        // was started from the file that is open.
        var mine = Container("a-redis-1", service: "redis", project: "other", configFiles: ComposeFile);
        var theirs = Container("b-redis-1", service: "redis", project: "dockercity");

        var matched = RuntimeMatcher.Match([Service("redis")], "dockercity", ComposeFile, [theirs, mine]);

        Assert.Equal("a-redis-1", matched["redis"].Name);
    }

    [Fact]
    public void Several_config_files_in_one_label_are_all_considered()
    {
        var container = Container(
            "dockercity-redis-1",
            service: "redis",
            configFiles: $@"C:\work\dockercity\docker-compose.yml,C:\work\dockercity\docker-compose.override.yml");

        var matched = RuntimeMatcher.Match([Service("redis")], null, ComposeFile, [container]);

        Assert.Single(matched);
    }

    [Fact]
    public void Compose_labels_match_within_the_same_project()
    {
        var container = Container("dockercity-redis-1", service: "redis", project: "dockercity");

        var matched = RuntimeMatcher.Match([Service("redis")], "dockercity", composeFilePath: null, [container]);

        Assert.Equal("dockercity-redis-1", matched["redis"].Name);
    }

    [Fact]
    public void Another_projects_service_of_the_same_name_is_not_ours()
    {
        var container = Container("shop-redis-1", service: "redis", project: "shop");

        var matched = RuntimeMatcher.Match([Service("redis")], "dockercity", ComposeFile, [container]);

        Assert.Empty(matched);
    }

    [Fact]
    public void Without_a_project_name_a_bare_service_label_is_not_enough()
    {
        var container = Container("shop-redis-1", service: "redis", project: "shop");

        var matched = RuntimeMatcher.Match([Service("redis")], projectName: null, composeFilePath: null, [container]);

        Assert.Empty(matched);
    }

    [Fact]
    public void An_explicit_container_name_matches_even_without_labels()
    {
        // Started by hand, or by an older Compose: no labels at all.
        var container = Container("cache-box");

        var matched = RuntimeMatcher.Match([Service("redis", containerName: "cache-box")], "dockercity", ComposeFile, [container]);

        Assert.Equal("cache-box", matched["redis"].Name);
    }

    [Theory]
    [InlineData("dockercity_redis_1")]
    [InlineData("dockercity-redis-1")]
    [InlineData("dockercity-redis-2")]
    public void Conventional_names_are_the_last_resort(string name)
    {
        var matched = RuntimeMatcher.Match([Service("redis")], "dockercity", ComposeFile, [Container(name)]);

        Assert.Equal(name, matched["redis"].Name);
    }

    [Fact]
    public void A_running_container_beats_yesterdays_leftover()
    {
        var old = Container("dockercity-redis-1", service: "redis", project: "dockercity", state: ContainerState.Exited);
        var current = Container("dockercity-redis-2", service: "redis", project: "dockercity");

        var matched = RuntimeMatcher.Match([Service("redis")], "dockercity", composeFilePath: null, [old, current]);

        Assert.Equal("dockercity-redis-2", matched["redis"].Name);
    }

    [Fact]
    public void A_stopped_container_is_still_reported()
    {
        var stopped = Container("dockercity-redis-1", service: "redis", project: "dockercity", state: ContainerState.Exited);

        var matched = RuntimeMatcher.Match([Service("redis")], "dockercity", composeFilePath: null, [stopped]);

        // Silence would look the same as "no container at all"; the figure
        // should be able to show a grey light instead.
        Assert.Equal(ContainerState.Exited, matched["redis"].State);
    }

    [Fact]
    public void Services_without_a_container_are_simply_absent()
    {
        var container = Container("dockercity-redis-1", service: "redis", project: "dockercity");

        var matched = RuntimeMatcher.Match([Service("redis"), Service("postgres")], "dockercity", null, [container]);

        Assert.Contains("redis", matched);
        Assert.DoesNotContain("postgres", matched);
    }

    [Fact]
    public void Nothing_running_matches_nothing()
    {
        var matched = RuntimeMatcher.Match([Service("redis")], "dockercity", ComposeFile, []);

        Assert.Empty(matched);
    }
}
