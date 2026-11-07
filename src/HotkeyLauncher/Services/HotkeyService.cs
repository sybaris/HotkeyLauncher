using System.Runtime.InteropServices;
using System.Windows.Interop;
using HotkeyLauncher.Core.Models;

namespace HotkeyLauncher.Services;

/// <summary>Registers system-wide hotkeys (RegisterHotKey) on a hidden message-only window.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;
    private const int ProbeId = 0xBFFE;

    private readonly HwndSource _source;
    private readonly Dictionary<int, Guid> _registered = [];

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("HotkeyLauncher.Hotkeys")
        {
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE
            WindowStyle = 0,
            Width = 0,
            Height = 0,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public event EventHandler<Guid>? HotkeyPressed;

    /// <summary>The quick launcher hotkey was pressed.</summary>
    public event EventHandler? QuickLauncherPressed;

    /// <summary>Key of <see cref="RegisterAll"/> failures for the quick launcher hotkey.</summary>
    public static readonly Guid QuickLauncherId = Guid.Empty;

    private const int QuickLauncherHotkeyId = 0xB000;
    private bool _quickLauncherRegistered;

    /// <summary>
    /// Registers the enabled entries (and the quick launcher hotkey, if any).
    /// Returns the entries that failed, with the Win32 error code (<see cref="QuickLauncherId"/> for the quick launcher).
    /// </summary>
    public IReadOnlyDictionary<Guid, int> RegisterAll(IEnumerable<HotkeyEntry> entries, Hotkey? quickLauncher = null)
    {
        UnregisterAll();
        var failures = new Dictionary<Guid, int>();
        if (quickLauncher is { IsComplete: true } ql)
        {
            _quickLauncherRegistered = RegisterHotKey(_source.Handle, QuickLauncherHotkeyId, (uint)ql.Modifiers | MOD_NOREPEAT, ql.VirtualKey);
            if (!_quickLauncherRegistered) failures[QuickLauncherId] = Marshal.GetLastPInvokeError();
        }

        var id = 1;
        foreach (var entry in entries.Where(e => e.IsEnabled && e.Hotkey.IsComplete))
        {
            if (RegisterHotKey(_source.Handle, id, (uint)entry.Modifiers | MOD_NOREPEAT, entry.VirtualKey))
            {
                _registered[id++] = entry.Id;
            }
            else
            {
                failures[entry.Id] = Marshal.GetLastPInvokeError();
            }
        }

        return failures;
    }

    public void UnregisterAll()
    {
        foreach (var id in _registered.Keys) UnregisterHotKey(_source.Handle, id);
        _registered.Clear();
        if (_quickLauncherRegistered) UnregisterHotKey(_source.Handle, QuickLauncherHotkeyId);
        _quickLauncherRegistered = false;
    }

    /// <summary>
    /// True if no other program (or Windows) has registered this combination. Our own hotkeys must be
    /// unregistered first, otherwise they are reported as taken.
    /// </summary>
    public bool IsAvailable(Hotkey hotkey)
    {
        if (!hotkey.IsComplete) return true;
        if (!RegisterHotKey(_source.Handle, ProbeId, (uint)hotkey.Modifiers | MOD_NOREPEAT, hotkey.VirtualKey)) return false;
        UnregisterHotKey(_source.Handle, ProbeId);
        return true;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_HOTKEY) return IntPtr.Zero;

        if (wParam.ToInt32() == QuickLauncherHotkeyId)
        {
            handled = true;
            QuickLauncherPressed?.Invoke(this, EventArgs.Empty);
        }
        else if (_registered.TryGetValue(wParam.ToInt32(), out var entryId))
        {
            handled = true;
            HotkeyPressed?.Invoke(this, entryId);
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
