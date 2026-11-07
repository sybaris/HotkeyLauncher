using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HotkeyLauncher.Core.Models;
using HotkeyLauncher.Core.Native;

namespace HotkeyLauncher.Core.Launching;

/// <summary>Brings an already running program to the front, and moves the window of a launched program.</summary>
public static class WindowManager
{
    /// <summary>
    /// Activates a visible window of a running instance of the program. Returns false when the program
    /// is not running (or has no visible window), so that the caller starts it.
    /// </summary>
    public static bool TryActivateExisting(string target)
    {
        var path = TargetResolver.ResolvePath(target);
        if (path == null || !File.Exists(path)) return false;

        var processIds = FindProcesses(path);
        if (processIds.Count == 0) return false;

        var window = FindMainWindow(processIds);
        if (window == IntPtr.Zero) return false;

        Activate(window);
        return true;
    }

    public static void Activate(IntPtr window)
    {
        if (WindowNative.IsIconic(window)) WindowNative.ShowWindow(window, WindowNative.SW_RESTORE);
        if (WindowNative.SetForegroundWindow(window)) return;

        // Windows may refuse the focus change: attach to the foreground thread to be allowed to do it
        var foregroundThread = WindowNative.GetWindowThreadProcessId(WindowNative.GetForegroundWindow(), out _);
        var currentThread = WindowNative.GetCurrentThreadId();
        WindowNative.AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            WindowNative.BringWindowToTop(window);
            WindowNative.SetForegroundWindow(window);
        }
        finally
        {
            WindowNative.AttachThreadInput(currentThread, foregroundThread, false);
        }
    }

    /// <summary>Waits (in the background) for the main window of the process, then moves it.</summary>
    public static Task PlaceWhenReadyAsync(Process process, HotkeyEntry entry) => Task.Run(async () =>
    {
        using (process)
        {
            try
            {
                var window = IntPtr.Zero;
                for (var i = 0; i < 50 && window == IntPtr.Zero; i++)
                {
                    await Task.Delay(200).ConfigureAwait(false);
                    if (process.HasExited) return;
                    process.Refresh();
                    window = process.MainWindowHandle;
                }

                if (window != IntPtr.Zero) Place(window, entry);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                Log.Warning($"Cannot place the window of \"{entry.Name}\": {ex.Message}");
            }
        }
    });

    public static void Place(IntPtr window, HotkeyEntry entry)
    {
        var screens = Screens.GetAll();
        if (screens.Count == 0) return;

        ScreenInfo screen;
        if (entry.Monitor > 0)
        {
            screen = screens[Math.Min(entry.Monitor, screens.Count) - 1];
        }
        else
        {
            // Screen currently containing the window
            var current = new WindowNative.MONITORINFOEX { cbSize = Marshal.SizeOf<WindowNative.MONITORINFOEX>() };
            WindowNative.GetMonitorInfo(WindowNative.MonitorFromWindow(window, WindowNative.MONITOR_DEFAULTTONEAREST), ref current);
            screen = screens.FirstOrDefault(s => s.X == current.rcMonitor.Left && s.Y == current.rcMonitor.Top) ?? screens[0];
        }

        if (WindowNative.IsZoomed(window) || WindowNative.IsIconic(window)) WindowNative.ShowWindow(window, WindowNative.SW_RESTORE);
        WindowNative.GetWindowRect(window, out var rect);

        var (x, y, width, height) = Screens.ComputeBounds(
            entry.Placement,
            (screen.WorkX, screen.WorkY, screen.WorkWidth, screen.WorkHeight),
            (rect.Width, rect.Height));

        // Windows 10/11 windows have an invisible resize border: compensate it so that halves touch the edges
        if (WindowNative.DwmGetWindowAttribute(window, WindowNative.DWMWA_EXTENDED_FRAME_BOUNDS, out var frame, Marshal.SizeOf<WindowNative.RECT>()) == 0)
        {
            var left = frame.Left - rect.Left;
            var right = rect.Right - frame.Right;
            var bottom = rect.Bottom - frame.Bottom;
            x -= left;
            width += left + right;
            height += bottom;
        }

        WindowNative.SetWindowPos(window, IntPtr.Zero, x, y, width, height, WindowNative.SWP_NOZORDER | WindowNative.SWP_NOACTIVATE);

        if (entry.WindowState == LaunchWindowState.Maximized) WindowNative.ShowWindow(window, WindowNative.SW_MAXIMIZE);
    }

    private static HashSet<uint> FindProcesses(string path)
    {
        var result = new HashSet<uint>();
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(path)))
        {
            using (process)
            {
                try
                {
                    if (string.Equals(process.MainModule?.FileName, path, StringComparison.OrdinalIgnoreCase)) result.Add((uint)process.Id);
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    // Elevated or protected process: the path is not readable, trust the name
                    result.Add((uint)process.Id);
                }
            }
        }

        return result;
    }

    /// <summary>First visible, unowned, titled top-level window of one of the processes.</summary>
    private static IntPtr FindMainWindow(HashSet<uint> processIds)
    {
        var found = IntPtr.Zero;
        WindowNative.EnumWindows((hWnd, _) =>
        {
            WindowNative.GetWindowThreadProcessId(hWnd, out var pid);
            if (processIds.Contains(pid)
                && WindowNative.IsWindowVisible(hWnd)
                && WindowNative.GetWindow(hWnd, WindowNative.GW_OWNER) == IntPtr.Zero
                && WindowNative.GetWindowTextLength(hWnd) > 0)
            {
                found = hWnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return found;
    }
}
