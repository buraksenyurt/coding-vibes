using Microsoft.UI.Dispatching;

namespace DockerCity.App.Services;

// Watches the open compose file and reports changes on the UI thread.
//
// FileSystemWatcher raises its events on a thread-pool thread; touching a
// view model from there would throw. The dispatcher queue hops back.
// Editors rarely just write in place: many save to a temp file and rename it
// over the original, so Created and Renamed matter as much as Changed.
public sealed class ComposeFileWatcher(DispatcherQueue dispatcher) : IDisposable
{
    private FileSystemWatcher? _watcher;

    public event EventHandler? Changed;

    public void Watch(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Stop();

        var folder = Path.GetDirectoryName(path);

        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return;
        }

        _watcher = new FileSystemWatcher(folder, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
        };

        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
        _watcher.EnableRaisingEvents = true;
    }

    public void Stop()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnFileEvent(object sender, FileSystemEventArgs args) =>
        dispatcher.TryEnqueue(() => Changed?.Invoke(this, EventArgs.Empty));

    public void Dispose() => Stop();
}
