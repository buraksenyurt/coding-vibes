using DockerCity.Domain.Values;

namespace DockerCity.Domain.Tests;

public class EnvVariableTests
{
    [Theory]
    [InlineData("POSTGRES_PASSWORD")]
    [InlineData("FTP_PASS")]
    [InlineData("MINIO_ROOT_PASSWORD")]
    [InlineData("api_key")]
    public void Flags_sensitive_keys(string key)
    {
        Assert.True(new EnvVariable(key, "hunter2").IsSensitive);
    }

    [Theory]
    [InlineData("POSTGRES_USER")]
    [InlineData("POSTGRES_DB")]
    [InlineData("PASV_MIN_PORT")]
    public void Leaves_ordinary_keys_alone(string key)
    {
        Assert.False(new EnvVariable(key, "value").IsSensitive);
    }

    [Fact]
    public void Masks_the_value_of_a_sensitive_key()
    {
        Assert.DoesNotContain("hunter2", new EnvVariable("DB_PASSWORD", "hunter2").DisplayValue);
        Assert.Equal("johndoe", new EnvVariable("POSTGRES_USER", "johndoe").DisplayValue);
    }

    [Fact]
    public void Renders_a_missing_value_as_empty_text()
    {
        Assert.Equal(string.Empty, new EnvVariable("INHERITED_FROM_HOST", null).DisplayValue);
    }
}
