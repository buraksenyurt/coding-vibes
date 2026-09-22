using DockerCity.Domain.Runtime;

namespace DockerCity.Domain.Tests;

public class ContainerSnapshotTests
{
    [Theory]
    [InlineData("running", ContainerState.Running)]
    [InlineData("Exited", ContainerState.Exited)]
    [InlineData("paused", ContainerState.Paused)]
    [InlineData("restarting", ContainerState.Restarting)]
    [InlineData("created", ContainerState.Created)]
    [InlineData("dead", ContainerState.Dead)]
    [InlineData("", ContainerState.Unknown)]
    [InlineData(null, ContainerState.Unknown)]
    public void States_are_read_from_the_daemon(string? state, ContainerState expected)
    {
        Assert.Equal(expected, ContainerSnapshot.ParseState(state));
    }

    [Theory]
    [InlineData("Up 3 hours (healthy)", HealthState.Healthy)]
    [InlineData("Up 2 minutes (unhealthy)", HealthState.Unhealthy)]
    [InlineData("Up 5 seconds (health: starting)", HealthState.Starting)]
    [InlineData("Up 3 hours", HealthState.None)]
    [InlineData("Exited (0) 2 days ago", HealthState.None)]
    [InlineData(null, HealthState.None)]
    public void Health_is_read_from_the_status_text(string? status, HealthState expected)
    {
        Assert.Equal(expected, ContainerSnapshot.ParseHealth(status));
    }

    [Fact]
    public void The_first_name_is_taken_without_its_slash()
    {
        var snapshot = ContainerSnapshot.From("abc", ["/dockercity-redis-1", "/alias"], null, "running", "Up 1 hour", "redis:latest");

        Assert.Equal("dockercity-redis-1", snapshot.Name);
    }

    [Fact]
    public void Compose_labels_are_picked_up()
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ContainerSnapshot.ProjectLabel] = "dockercity",
            [ContainerSnapshot.ServiceLabel] = "redis",
            [ContainerSnapshot.ConfigFilesLabel] = @"C:\work\dockercity\docker-compose.yml"
        };

        var snapshot = ContainerSnapshot.From("abc", ["/dockercity-redis-1"], labels, "running", "Up 1 hour", "redis:latest");

        Assert.Equal("dockercity", snapshot.ProjectName);
        Assert.Equal("redis", snapshot.ServiceName);
        Assert.Equal(@"C:\work\dockercity\docker-compose.yml", snapshot.ConfigFiles);
    }

    [Fact]
    public void A_container_without_compose_labels_keeps_nulls()
    {
        var snapshot = ContainerSnapshot.From("abc", ["/lonely"], new Dictionary<string, string>(), "running", "Up 1 hour", "redis");

        Assert.Null(snapshot.ProjectName);
        Assert.Null(snapshot.ServiceName);
        Assert.Null(snapshot.ConfigFiles);
    }

    [Theory]
    [InlineData(ContainerState.Running, HealthState.None, RuntimeSignal.Running)]
    [InlineData(ContainerState.Running, HealthState.Healthy, RuntimeSignal.Healthy)]
    [InlineData(ContainerState.Running, HealthState.Unhealthy, RuntimeSignal.Unhealthy)]
    [InlineData(ContainerState.Running, HealthState.Starting, RuntimeSignal.Starting)]
    [InlineData(ContainerState.Restarting, HealthState.None, RuntimeSignal.Starting)]
    [InlineData(ContainerState.Paused, HealthState.None, RuntimeSignal.Paused)]
    [InlineData(ContainerState.Exited, HealthState.None, RuntimeSignal.Stopped)]
    [InlineData(ContainerState.Created, HealthState.None, RuntimeSignal.Stopped)]
    [InlineData(ContainerState.Unknown, HealthState.None, RuntimeSignal.Unknown)]
    public void State_and_health_become_one_traffic_light(ContainerState state, HealthState health, RuntimeSignal expected)
    {
        var snapshot = new ContainerSnapshot("abc", "x") { State = state, Health = health };

        Assert.Equal(expected, snapshot.Signal);
    }

    [Fact]
    public void An_unhealthy_container_is_still_up()
    {
        var snapshot = new ContainerSnapshot("abc", "x")
        {
            State = ContainerState.Running,
            Health = HealthState.Unhealthy
        };

        // It answers the port, it just fails its own check: the count of
        // running services should not quietly drop it.
        Assert.True(snapshot.IsUp);
    }
}
