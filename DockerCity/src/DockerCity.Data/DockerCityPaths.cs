namespace DockerCity.Data;

public static class DockerCityPaths
{
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DockerCity");

    public static string DatabaseFile => Path.Combine(DataDirectory, "dockercity.db");

    public static string ConnectionString => ToConnectionString(DatabaseFile);

    public static string ToConnectionString(string databaseFile) => $"Data Source={databaseFile}";
}
