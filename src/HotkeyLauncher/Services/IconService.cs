using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HotkeyLauncher.Core.Launching;

namespace HotkeyLauncher.Services;

/// <summary>Icons of targets (program icon, folder icon, browser icon for URLs), cached.</summary>
public static class IconService
{
    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;
    private const uint SHGFI_SMALLICON = 0x1;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    private static readonly Dictionary<string, ImageSource?> ImageCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, System.Drawing.Bitmap?> BitmapCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>32 px icon for the WPF windows.</summary>
    public static ImageSource? GetImage(string target)
    {
        if (ImageCache.TryGetValue(target, out var cached)) return cached;

        ImageSource? image = null;
        var hIcon = GetIconHandle(target, small: false);
        if (hIcon != IntPtr.Zero)
        {
            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                image = source;
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }

        return ImageCache[target] = image;
    }

    /// <summary>16 px icon for the notification-area menus.</summary>
    public static System.Drawing.Bitmap? GetMenuBitmap(string target)
    {
        if (BitmapCache.TryGetValue(target, out var cached)) return cached;

        System.Drawing.Bitmap? bitmap = null;
        var hIcon = GetIconHandle(target, small: true);
        if (hIcon != IntPtr.Zero)
        {
            try
            {
                using var icon = System.Drawing.Icon.FromHandle(hIcon);
                bitmap = icon.ToBitmap();
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }

        return BitmapCache[target] = bitmap;
    }

    public static void ClearCache()
    {
        ImageCache.Clear();
        foreach (var bitmap in BitmapCache.Values) bitmap?.Dispose();
        BitmapCache.Clear();
    }

    private static IntPtr GetIconHandle(string target, bool small)
    {
        if (string.IsNullOrWhiteSpace(target)) return IntPtr.Zero;

        var flags = SHGFI_ICON | (small ? SHGFI_SMALLICON : SHGFI_LARGEICON);
        var info = new SHFILEINFO();
        IntPtr result;
        if (TargetResolver.IsUrl(target))
        {
            // Icon of the program associated to web links (the default browser)
            var extension = target.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? ".html" : ".url";
            result = SHGetFileInfo(extension, FILE_ATTRIBUTE_NORMAL, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags | SHGFI_USEFILEATTRIBUTES);
        }
        else
        {
            var path = TargetResolver.ResolvePath(target);
            if (path == null) return IntPtr.Zero;
            result = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
        }

        return result == IntPtr.Zero ? IntPtr.Zero : info.hIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
