using HotkeyLauncher.Core.Models;
using HotkeyLauncher.Core.Native;

namespace HotkeyLauncher.Core.Launching;

/// <summary>A screen, in physical pixels.</summary>
public sealed record ScreenInfo(int Number, bool IsPrimary, int X, int Y, int Width, int Height, int WorkX, int WorkY, int WorkWidth, int WorkHeight);

public static class Screens
{
    /// <summary>Screens numbered from 1: the main screen first, then the others from left to right.</summary>
    public static IReadOnlyList<ScreenInfo> GetAll()
    {
        var raw = new List<(bool primary, WindowNative.RECT bounds, WindowNative.RECT work)>();
        WindowNative.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr _, ref WindowNative.RECT _, IntPtr _) =>
        {
            var info = new WindowNative.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<WindowNative.MONITORINFOEX>() };
            if (WindowNative.GetMonitorInfo(monitor, ref info))
            {
                raw.Add(((info.dwFlags & WindowNative.MONITORINFOF_PRIMARY) != 0, info.rcMonitor, info.rcWork));
            }

            return true;
        }, IntPtr.Zero);

        return [.. raw
            .OrderByDescending(m => m.primary)
            .ThenBy(m => m.bounds.Left)
            .ThenBy(m => m.bounds.Top)
            .Select((m, i) => new ScreenInfo(i + 1, m.primary,
                m.bounds.Left, m.bounds.Top, m.bounds.Width, m.bounds.Height,
                m.work.Left, m.work.Top, m.work.Width, m.work.Height))];
    }

    /// <summary>Target rectangle (X, Y, Width, Height) of a window inside a work area.</summary>
    public static (int X, int Y, int Width, int Height) ComputeBounds(
        WindowPlacement placement, (int X, int Y, int Width, int Height) workArea, (int Width, int Height) currentSize)
    {
        var (x, y, w, h) = workArea;
        return placement switch
        {
            WindowPlacement.LeftHalf => (x, y, w / 2, h),
            WindowPlacement.RightHalf => (x + w - w / 2, y, w / 2, h),
            WindowPlacement.TopHalf => (x, y, w, h / 2),
            WindowPlacement.BottomHalf => (x, y + h - h / 2, w, h / 2),
            // Center (and Default when only the screen is chosen): keep the size, at most the work area
            _ => CenterIn(workArea, Math.Min(currentSize.Width, w), Math.Min(currentSize.Height, h)),
        };
    }

    private static (int, int, int, int) CenterIn((int X, int Y, int Width, int Height) area, int width, int height) =>
        (area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height);
}
