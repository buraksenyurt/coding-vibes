using DockerCity.Domain.Runtime;

namespace DockerCity.Domain.Tests;

public class ComposeProjectNameTests
{
    [Theory]
    [InlineData("DockerCity", "dockercity")]
    [InlineData("my compose_1", "mycompose_1")]
    [InlineData("web-api", "web-api")]
    [InlineData("_private", "private")]
    [InlineData("2023 demo", "2023demo")]
    [InlineData("şehir", "ehir")]
    public void Folder_names_are_normalised_the_way_compose_does_it(string folder, string expected)
    {
        Assert.Equal(expected, ComposeProjectName.Normalize(folder));
    }

    [Fact]
    public void The_project_name_comes_from_the_folder_not_the_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "Sample Project", "docker-compose.yml");

        Assert.Equal("sampleproject", ComposeProjectName.FromPath(path));
    }
}
