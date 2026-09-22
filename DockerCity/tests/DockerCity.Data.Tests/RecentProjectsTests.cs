using DockerCity.Data.Stores;

namespace DockerCity.Data.Tests;

public class RecentProjectsTests
{
    private const string First = @"C:\work\one\docker-compose.yml";
    private const string Second = @"C:\work\two\docker-compose.yml";

    [Fact]
    public async Task Clearing_recent_hides_projects_but_keeps_their_layout()
    {
        using var database = new TemporaryDatabase();
        var projects = new ComposeProjectStore(database.Context);
        var layouts = new LayoutStore(database.Context);

        var project = await projects.OpenAsync(First);
        await layouts.SaveAsync(project.Id, "postgres", new ServicePosition(10, 20));

        var hidden = await projects.ClearRecentAsync();

        Assert.Equal(1, hidden);
        Assert.Empty(await projects.RecentAsync());

        // The whole point of hiding instead of deleting.
        Assert.Equal(new ServicePosition(10, 20), (await layouts.LoadAsync(project.Id))["postgres"]);
    }

    [Fact]
    public async Task Opening_a_hidden_project_puts_it_back_on_the_list()
    {
        using var database = new TemporaryDatabase();
        var projects = new ComposeProjectStore(database.Context);

        var original = await projects.OpenAsync(First);
        await projects.ClearRecentAsync();

        var reopened = await projects.OpenAsync(First);

        Assert.Equal(original.Id, reopened.Id);
        Assert.Single(await projects.RecentAsync());
    }

    [Fact]
    public async Task Removing_one_path_leaves_the_others()
    {
        using var database = new TemporaryDatabase();
        var projects = new ComposeProjectStore(database.Context);

        await projects.OpenAsync(First);
        await projects.OpenAsync(Second);

        Assert.Equal(1, await projects.RemoveFromRecentAsync(First));

        var recent = await projects.RecentAsync();
        Assert.Equal(Second, Assert.Single(recent).FilePath);
    }

    [Fact]
    public async Task Removing_an_unknown_path_changes_nothing()
    {
        using var database = new TemporaryDatabase();
        var projects = new ComposeProjectStore(database.Context);

        await projects.OpenAsync(First);

        Assert.Equal(0, await projects.RemoveFromRecentAsync(@"C:\nowhere\docker-compose.yml"));
        Assert.Single(await projects.RecentAsync());
    }

    [Fact]
    public async Task Find_returns_the_hash_from_the_last_visit()
    {
        using var database = new TemporaryDatabase();
        var projects = new ComposeProjectStore(database.Context);

        Assert.Null(await projects.FindAsync(First));

        await projects.OpenAsync(First, fileHash: "AAA");
        Assert.Equal("AAA", (await projects.FindAsync(First))!.FileHash);

        await projects.OpenAsync(First, fileHash: "BBB");
        Assert.Equal("BBB", (await projects.FindAsync(First))!.FileHash);
    }
}
