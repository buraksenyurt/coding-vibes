using DockerCity.Domain.Runtime;
using DockerCity.Live;
using Microsoft.UI.Dispatching;

namespace DockerCity.App.Services;

// What one poll came back with. A failure is a result too: the city should
// say "Docker is not reachable", not keep showing last minute's lights.
public sealed record RuntimeUpdate(bool IsConnected, IReadOnlyList<ContainerSnapshot> Containers, string? Error)
{
    public static RuntimeUpdate Connected(IReadOnlyList<ContainerSnapshot> containers) => new(true, containers, null);

    public static RuntimeUpdate Offline(string error) => new(false, [], error);
}

// Asks Docker what is running, over and over, on the UI thread's timer.
//
// Docker has no "tell me when something changes" that is worth the trouble
// here (the events stream needs a long-lived connection and its own failure
// handling), so this polls. Politely: it backs off when nobody answers, skips
// a turn if the previous one is still in flight, and stops when the window is
// not in front of the user.
public sealed class RuntimeMonitor : IDisposable
{
    private static readonly TimeSpan Fast = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan Slow = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly DispatcherQueueTimer _timer;
    private readonly Func<IContainerProbe> _open;

    private IContainerProbe? _probe;
    private bool _polling;

    // The factory is a parameter so a test - or a later "connect to another
    // machine" feature - can hand in something other than the local daemon.
    public RuntimeMonitor(DispatcherQueue queue, Func<IContainerProbe>? open = null)
    {
        ArgumentNullException.ThrowIfNull(queue);

        _open = open ?? (() => new DockerContainerProbe());

        _timer = queue.CreateTimer();
        _timer.Interval = Fast;
        _timer.Tick += (_, _) => _ = PollAsync();
    }

    public event EventHandler<RuntimeUpdate>? Updated;

    public bool IsRunning => _timer.IsRunning;

    public void Start()
    {
        if (_timer.IsRunning)
        {
            return;
        }

        _timer.Start();

        // Do not make the user wait three seconds for the first answer.
        _ = PollAsync();
    }

    public void Stop() => _timer.Stop();

    private async Task PollAsync()
    {
        // A daemon that takes four seconds must not end up with two polls in
        // the air and answers arriving out of order.
        if (_polling)
        {
            return;
        }

        _polling = true;

        try
        {
            _probe ??= _open();

            using var patience = new CancellationTokenSource(Patience);
            var containers = await _probe.ListAsync(patience.Token);

            _timer.Interval = Fast;
            Updated?.Invoke(this, RuntimeUpdate.Connected(containers));
        }
        catch (Exception exception)
        {
            // The client holds on to a broken connection, so it is thrown
            // away and rebuilt on the next turn.
            _probe?.Dispose();
            _probe = null;

            // Docker is probably not running. Knocking every three seconds
            // would not make it start any sooner.
            _timer.Interval = Slow;
            Updated?.Invoke(this, RuntimeUpdate.Offline(Describe(exception)));
        }
        finally
        {
            _polling = false;
        }
    }

    private static string Describe(Exception exception) => exception is OperationCanceledException
        ? "Docker did not answer in time."
        : exception.Message;

    public void Dispose()
    {
        _timer.Stop();
        _probe?.Dispose();
    }
}
