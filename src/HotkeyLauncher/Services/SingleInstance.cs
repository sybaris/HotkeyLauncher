namespace HotkeyLauncher.Services;

/// <summary>
/// Only one HotkeyLauncher per Windows session (two instances would fight for the same hotkeys).
/// Starting it again simply brings the running instance's window to the front.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string BaseName = @"Local\HotkeyLauncher.sybaris";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private RegisteredWaitHandle? _waitHandle;

    public SingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: true, BaseName + ".Instance", out var createdNew);
        IsFirstInstance = createdNew;
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, BaseName + ".Show");
    }

    public bool IsFirstInstance { get; }

    public void SignalFirstInstance() => _showEvent.Set();

    /// <summary>Calls <paramref name="onShowRequested"/> (on a pool thread) each time another instance is started.</summary>
    public void Listen(Action onShowRequested) =>
        _waitHandle = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) => onShowRequested(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        _waitHandle?.Unregister(null);
        _showEvent.Dispose();
        if (IsFirstInstance) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
