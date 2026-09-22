using System.Globalization;

namespace DockerCity.App.ViewModels;

public sealed class RecentFileViewModel(string name, string filePath, DateTimeOffset lastOpenedAt)
{
    public string Name { get; } = name;

    public string FilePath { get; } = filePath;

    public DateTimeOffset LastOpenedAt { get; } = lastOpenedAt;

    public string LastOpenedText => LastOpenedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public string MenuText => $"{Name}  —  {FilePath}";
}
