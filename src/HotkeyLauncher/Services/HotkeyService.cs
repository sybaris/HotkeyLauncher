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

    /// <summary>Registers the enabled entries. Returns the entries that failed, with the Win32 error code.</summary>
    public IReadOnlyDictionary<Guid, int> RegisterAll(IEnumerable<HotkeyEntry> entries)
    {
        UnregisterAll();
        var failures = new Dictionary<Guid, int>();
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
        if (msg == WM_HOTKEY && _registered.TryGetValue(wParam.ToInt32(), out var entryId))
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
