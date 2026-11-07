using HotkeyLauncher.Core.Models;
using HotkeyLauncher.Core.Native;

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
    public static KeyDefinition Vk(uint vk, double width = 1, string? label = null) => new(KeyKind.Normal, width, VirtualKey: vk, Label: label);
    public static KeyDefinition Mod(HotkeyModifiers modifier, double width) => new(KeyKind.Modifier, width, Modifier: modifier);
    public static KeyDefinition Deco(string label, double width) => new(KeyKind.Decorative, width, Label: label);
    public static KeyDefinition Space(double width) => new(KeyKind.Spacer, width);

    /// <summary>Virtual key for the current keyboard layout (0 for non selectable keys).</summary>
    public uint ResolveVirtualKey() => Kind != KeyKind.Normal ? 0 : VirtualKey != 0 ? VirtualKey : KeyNames.VirtualKeyFromScanCode(ScanCode);
}

/// <summary>Geometry of the on-screen keyboard. Widths are in key units; every row of a block has the same total width.</summary>
public static class KeyboardLayout
{
    // Languages whose keyboards are usually ANSI (US shape): English (US), Chinese, Korean
    private static readonly HashSet<int> AnsiLanguages = [0x0409, 0x0404, 0x0804, 0x0C04, 0x1004, 0x0412];

    /// <summary>Resolves <see cref="KeyboardShape.Auto"/> from the current Windows keyboard layout.</summary>
    public static KeyboardShape Resolve(KeyboardShape shape)
    {
        if (shape != KeyboardShape.Auto) return shape;
        var language = (int)(NativeMethods.GetKeyboardLayout(0).ToInt64() & 0xFFFF);
        return AnsiLanguages.Contains(language) ? KeyboardShape.Ansi : KeyboardShape.Iso;
    }

    public static IReadOnlyList<IReadOnlyList<KeyDefinition>> MainBlock(KeyboardShape shape) =>
        Resolve(shape) == KeyboardShape.Ansi ? AnsiMainBlock : IsoMainBlock;

    private static IReadOnlyList<KeyDefinition> FunctionRow { get; } =
    [
        KeyDefinition.Deco("Esc", 1), KeyDefinition.Space(1),
        .. FKeys(1, 4), KeyDefinition.Space(0.5), .. FKeys(5, 8), KeyDefinition.Space(0.5), .. FKeys(9, 12),
    ];

    private static IReadOnlyList<KeyDefinition> BottomRow { get; } =
    [
        KeyDefinition.Mod(HotkeyModifiers.Control, 1.25), KeyDefinition.Mod(HotkeyModifiers.Win, 1.25), KeyDefinition.Mod(HotkeyModifiers.Alt, 1.25),
        KeyDefinition.Vk(0x20, 6.25),
        KeyDefinition.Deco("AltGr", 1.25), KeyDefinition.Mod(HotkeyModifiers.Win, 1.25), KeyDefinition.Deco("☰", 1.25), KeyDefinition.Mod(HotkeyModifiers.Control, 1.25),
    ];

    /// <summary>European keyboards: extra key next to left Shift, tall Enter.</summary>
    public static IReadOnlyList<IReadOnlyList<KeyDefinition>> IsoMainBlock { get; } =
    [
        FunctionRow,
        [KeyDefinition.Scan(0x29), .. ScanRange(0x02, 0x0D), KeyDefinition.Deco("⌫", 2)],
        [KeyDefinition.Deco("⇥", 1.5), .. ScanRange(0x10, 0x1B), KeyDefinition.Deco("↵", 1.5)],
        [KeyDefinition.Deco("⇪", 1.75), .. ScanRange(0x1E, 0x28), KeyDefinition.Scan(0x2B), KeyDefinition.Deco("", 1.25)],
        [KeyDefinition.Mod(HotkeyModifiers.Shift, 1.25), KeyDefinition.Scan(0x56), .. ScanRange(0x2C, 0x35), KeyDefinition.Mod(HotkeyModifiers.Shift, 2.75)],
        BottomRow,
    ];

    /// <summary>US keyboards: backslash above Enter, wide left Shift.</summary>
    public static IReadOnlyList<IReadOnlyList<KeyDefinition>> AnsiMainBlock { get; } =
    [
        FunctionRow,
        [KeyDefinition.Scan(0x29), .. ScanRange(0x02, 0x0D), KeyDefinition.Deco("⌫", 2)],
        [KeyDefinition.Deco("⇥", 1.5), .. ScanRange(0x10, 0x1B), KeyDefinition.Scan(0x2B, 1.5)],
        [KeyDefinition.Deco("⇪", 1.75), .. ScanRange(0x1E, 0x28), KeyDefinition.Deco("↵", 2.25)],
        [KeyDefinition.Mod(HotkeyModifiers.Shift, 2.25), .. ScanRange(0x2C, 0x35), KeyDefinition.Mod(HotkeyModifiers.Shift, 2.75)],
        BottomRow,
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

    /// <summary>Numeric keypad (its keys produce hotkeys only while Num Lock is on).</summary>
    public static IReadOnlyList<IReadOnlyList<KeyDefinition>> NumpadBlock { get; } =
    [
        [KeyDefinition.Space(4)],
        [KeyDefinition.Deco("Num", 1), KeyDefinition.Vk(0x6F, 1, "/"), KeyDefinition.Vk(0x6A, 1, "*"), KeyDefinition.Vk(0x6D, 1, "-")],
        [KeyDefinition.Vk(0x67, 1, "7"), KeyDefinition.Vk(0x68, 1, "8"), KeyDefinition.Vk(0x69, 1, "9"), KeyDefinition.Vk(0x6B, 1, "+")],
        [KeyDefinition.Vk(0x64, 1, "4"), KeyDefinition.Vk(0x65, 1, "5"), KeyDefinition.Vk(0x66, 1, "6"), KeyDefinition.Space(1)],
        [KeyDefinition.Vk(0x61, 1, "1"), KeyDefinition.Vk(0x62, 1, "2"), KeyDefinition.Vk(0x63, 1, "3"), KeyDefinition.Deco("↵", 1)],
        [KeyDefinition.Vk(0x60, 2, "0"), KeyDefinition.Vk(0x6E, 1, "."), KeyDefinition.Space(1)],
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
