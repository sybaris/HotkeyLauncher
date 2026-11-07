using HotkeyLauncher.Core.Models;

namespace HotkeyLauncher.Core;

public enum KeyKind
{
    /// <summary>A key usable in a hotkey.</summary>
    Normal,
    /// <summary>Ctrl / Alt / Shift / Win: toggles a modifier.</summary>
    Modifier,
    /// <summary>Drawn for realism only (Tab, Caps Lock, Enter...), not selectable.</summary>
    Decorative,
    /// <summary>Empty space.</summary>
    Spacer,
}

/// <summary>
/// One physical key. Alphanumeric keys are described by their scan code (physical position), so that
/// the label and virtual key follow the user's layout: the key at "Q" position shows "A" on AZERTY.
/// </summary>
public sealed record KeyDefinition(KeyKind Kind, double Width, uint ScanCode = 0, uint VirtualKey = 0, string? Label = null, HotkeyModifiers Modifier = HotkeyModifiers.None)
{
    public static KeyDefinition Scan(uint scanCode, double width = 1) => new(KeyKind.Normal, width, ScanCode: scanCode);
    public static KeyDefinition Vk(uint vk, double width = 1) => new(KeyKind.Normal, width, VirtualKey: vk);
    public static KeyDefinition Mod(HotkeyModifiers modifier, double width) => new(KeyKind.Modifier, width, Modifier: modifier);
    public static KeyDefinition Deco(string label, double width) => new(KeyKind.Decorative, width, Label: label);
    public static KeyDefinition Space(double width) => new(KeyKind.Spacer, width);

    /// <summary>Virtual key for the current keyboard layout (0 for non selectable keys).</summary>
    public uint ResolveVirtualKey() => Kind != KeyKind.Normal ? 0 : VirtualKey != 0 ? VirtualKey : KeyNames.VirtualKeyFromScanCode(ScanCode);
}

/// <summary>Geometry of the on-screen keyboard (ISO 105 keys layout, the one of AZERTY keyboards). Widths are in key units.</summary>
public static class KeyboardLayout
{
    public static IReadOnlyList<IReadOnlyList<KeyDefinition>> MainBlock { get; } =
    [
        [
            KeyDefinition.Deco("Esc", 1), KeyDefinition.Space(1),
            .. FKeys(1, 4), KeyDefinition.Space(0.5), .. FKeys(5, 8), KeyDefinition.Space(0.5), .. FKeys(9, 12),
        ],
        [
            KeyDefinition.Scan(0x29), .. ScanRange(0x02, 0x0D), KeyDefinition.Deco("⌫", 2),
        ],
        [
            KeyDefinition.Deco("⇥", 1.5), .. ScanRange(0x10, 0x1B), KeyDefinition.Deco("↵", 1.5),
        ],
        [
            KeyDefinition.Deco("⇪", 1.75), .. ScanRange(0x1E, 0x28), KeyDefinition.Scan(0x2B), KeyDefinition.Deco("", 1.25),
        ],
        [
            KeyDefinition.Mod(HotkeyModifiers.Shift, 1.25), KeyDefinition.Scan(0x56), .. ScanRange(0x2C, 0x35), KeyDefinition.Mod(HotkeyModifiers.Shift, 2.75),
        ],
        [
            KeyDefinition.Mod(HotkeyModifiers.Control, 1.25), KeyDefinition.Mod(HotkeyModifiers.Win, 1.25), KeyDefinition.Mod(HotkeyModifiers.Alt, 1.25),
            KeyDefinition.Vk(0x20, 6.25),
            KeyDefinition.Deco("AltGr", 1.25), KeyDefinition.Mod(HotkeyModifiers.Win, 1.25), KeyDefinition.Deco("☰", 1.25), KeyDefinition.Mod(HotkeyModifiers.Control, 1.25),
        ],
    ];

    public static IReadOnlyList<IReadOnlyList<KeyDefinition>> NavigationBlock { get; } =
    [
        [KeyDefinition.Vk(0x2C), KeyDefinition.Vk(0x91), KeyDefinition.Vk(0x13)],          // PrtSc, Scroll Lock, Pause
        [KeyDefinition.Vk(0x2D), KeyDefinition.Vk(0x24), KeyDefinition.Vk(0x21)],          // Insert, Home, PageUp
        [KeyDefinition.Vk(0x2E), KeyDefinition.Vk(0x23), KeyDefinition.Vk(0x22)],          // Delete, End, PageDown
        [KeyDefinition.Space(3)],
        [KeyDefinition.Space(1), KeyDefinition.Vk(0x26), KeyDefinition.Space(1)],          // Up
        [KeyDefinition.Vk(0x25), KeyDefinition.Vk(0x28), KeyDefinition.Vk(0x27)],          // Left, Down, Right
    ];

    /// <summary>Keys proposed as suggestions, by order of preference.</summary>
    public static IEnumerable<uint> SuggestionCandidates()
    {
        for (uint vk = 0x41; vk <= 0x5A; vk++) yield return vk;   // A-Z
        for (uint vk = 0x31; vk <= 0x39; vk++) yield return vk;   // 1-9
        yield return 0x30;                                         // 0
        for (uint vk = 0x70; vk <= 0x7B; vk++) yield return vk;   // F1-F12
    }

    private static IEnumerable<KeyDefinition> FKeys(int from, int to) =>
        Enumerable.Range(from, to - from + 1).Select(n => KeyDefinition.Vk((uint)(0x70 + n - 1)));

    private static IEnumerable<KeyDefinition> ScanRange(uint from, uint to)
    {
        for (var sc = from; sc <= to; sc++) yield return KeyDefinition.Scan(sc);
    }
}
