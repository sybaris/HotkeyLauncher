using System.Text;
using HotkeyLauncher.Core.Models;
using HotkeyLauncher.Core.Native;

namespace HotkeyLauncher.Core;

/// <summary>Human readable names of keys, following the current keyboard layout (AZERTY, QWERTY...).</summary>
public static class KeyNames
{
    /// <summary>Modifier names, replaceable by the UI for localization (e.g. "Maj" for Shift in French).</summary>
    public static string CtrlName { get; set; } = "Ctrl";
    public static string AltName { get; set; } = "Alt";
    public static string ShiftName { get; set; } = "Shift";
    public static string WinName { get; set; } = "Win";

    // Keys whose scan code needs the "extended" flag for GetKeyNameText
    private static readonly HashSet<uint> ExtendedKeys =
    [
        0x21, 0x22, 0x23, 0x24, // PageUp, PageDown, End, Home
        0x25, 0x26, 0x27, 0x28, // arrows
        0x2D, 0x2E,             // Insert, Delete
        0x6F, 0x90,             // Numpad divide, NumLock
        0x5D,                   // Applications
    ];

    private static readonly Dictionary<uint, string> FixedNames = new()
    {
        [0x25] = "←",
        [0x26] = "↑",
        [0x27] = "→",
        [0x28] = "↓",
        [0x13] = "Pause",
        [0x2C] = "PrtSc",
    };

    /// <summary>Modifier names in display order.</summary>
    public static IEnumerable<string> ModifierNames(HotkeyModifiers modifiers)
    {
        if (modifiers.HasFlag(HotkeyModifiers.Control)) yield return CtrlName;
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) yield return AltName;
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) yield return ShiftName;
        if (modifiers.HasFlag(HotkeyModifiers.Win)) yield return WinName;
    }

    /// <summary>All parts of a hotkey ("Ctrl", "Alt", "N"), handy to draw key caps.</summary>
    public static IReadOnlyList<string> Parts(Hotkey hotkey) =>
        hotkey.IsEmpty ? [] : [.. ModifierNames(hotkey.Modifiers), GetKeyName(hotkey.VirtualKey)];

    public static string Format(Hotkey hotkey) => string.Join(" + ", Parts(hotkey));

    public static string GetKeyName(uint vk)
    {
        if (FixedNames.TryGetValue(vk, out var fixedName)) return fixedName;
        if (vk is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A) return ((char)vk).ToString();
        if (vk is >= 0x70 and <= 0x87) return $"F{vk - 0x70 + 1}";

        var layout = NativeMethods.GetKeyboardLayout(0);

        // Punctuation keys (VK_OEM_*): the character printed on the key for the current layout
        if (vk is >= 0xBA and <= 0xC0 or >= 0xDB and <= 0xDF or 0xE2)
        {
            var ch = NativeMethods.MapVirtualKeyEx(vk, NativeMethods.MAPVK_VK_TO_CHAR, layout) & 0xFFFF;
            if (ch != 0) return char.ToUpper((char)ch).ToString();
        }

        var scanCode = NativeMethods.MapVirtualKeyEx(vk, NativeMethods.MAPVK_VK_TO_VSC, layout);
        if (scanCode != 0)
        {
            var lParam = (int)(scanCode << 16);
            if (ExtendedKeys.Contains(vk)) lParam |= 1 << 24;
            var sb = new StringBuilder(64);
            if (NativeMethods.GetKeyNameText(lParam, sb, sb.Capacity) > 0) return Capitalize(sb.ToString());
        }

        return $"0x{vk:X2}";
    }

    /// <summary>Virtual key of a physical key (scan code) for the current keyboard layout, 0 if none.</summary>
    public static uint VirtualKeyFromScanCode(uint scanCode) =>
        NativeMethods.MapVirtualKeyEx(scanCode, NativeMethods.MAPVK_VSC_TO_VK, NativeMethods.GetKeyboardLayout(0));

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..].ToLower();
}
