using DockerCity.Data.Stores;

namespace DockerCity.Data.Tests;

public class StoreTests
{
    [Fact]
    public async Task Opening_the_same_file_twice_reuses_one_project_row()
    {
        using var database = new TemporaryDatabase();
        var store = new ComposeProjectStore(database.Context);

        var first = await store.OpenAsync(@"C:\work\shop\docker-compose.yml");
        var opened = first.LastOpenedAt;

        await Task.Delay(5);
        var second = await store.OpenAsync(@"C:\work\shop\docker-compose.yml");

        Assert.Equal(first.Id, second.Id);
        Assert.True(second.LastOpenedAt >= opened);
        Assert.Equal("shop", second.Name);
    }

    [Fact]
    public async Task Recent_projects_come_back_newest_first()
    {
        using var database = new TemporaryDatabase();
        var store = new ComposeProjectStore(database.Context);

        await store.OpenAsync(@"C:\work\one\docker-compose.yml");
        await Task.Delay(5);
        await store.OpenAsync(@"C:\work\two\docker-compose.yml");

        var recent = await store.RecentAsync();

        Assert.Equal("two", recent[0].Name);
        Assert.Equal("one", recent[1].Name);
    }

    [Fact]
    public async Task Layout_positions_survive_a_reopen()
    {
        using var database = new TemporaryDatabase();
        var project = await new ComposeProjectStore(database.Context)
            .OpenAsync(@"C:\work\shop\docker-compose.yml");

        await new LayoutStore(database.Context)
            .SaveAsync(project.Id, "postgres", new ServicePosition(120, 240, IsPinned: true));

        using var reopened = database.NewConnection();
        var positions = await new LayoutStore(reopened).LoadAsync(project.Id);

        Assert.Equal(new ServicePosition(120, 240, true), positions["postgres"]);
    }

    [Fact]
    public async Task Saving_a_position_twice_updates_instead_of_duplicating()
    {
        using var database = new TemporaryDatabase();
        var project = await new ComposeProjectStore(database.Context)
            .OpenAsync(@"C:\work\shop\docker-compose.yml");
        var layouts = new LayoutStore(database.Context);

        await layouts.SaveAsync(project.Id, "redis", new ServicePosition(10, 10));
        await layouts.SaveAsync(project.Id, "redis", new ServicePosition(90, 90));

        var positions = await layouts.LoadAsync(project.Id);

        Assert.Single(positions);
        Assert.Equal(new ServicePosition(90, 90), positions["redis"]);
    }

    [Fact]
    public async Task Settings_round_trip()
    {
        using var database = new TemporaryDatabase();
        var settings = new AppSettingsStore(database.Context);

        Assert.Null(await settings.GetAsync("theme"));

        await settings.SetAsync("theme", "dark");
        Assert.Equal("dark", await settings.GetAsync("theme"));

        await settings.SetAsync("theme", "light");
        Assert.Equal("light", await settings.GetAsync("theme"));
    }
}
