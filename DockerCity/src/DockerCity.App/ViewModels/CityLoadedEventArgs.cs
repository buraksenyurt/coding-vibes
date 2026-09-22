namespace DockerCity.App.ViewModels;

// Raised once the collections hold the new city. IsSameFile tells a reload
// (keep the zoom, no fanfare) from a newly opened file (fit it, animate it).
public sealed class CityLoadedEventArgs(bool isSameFile) : EventArgs
{
    public bool IsSameFile { get; } = isSameFile;
}
