namespace HotkeyLauncher.Core.Models;

/// <summary>Modifier keys of a hotkey. Values match the Win32 MOD_* constants used by RegisterHotKey.</summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

/// <summary>A keyboard combination: modifiers + one virtual key.</summary>
public readonly record struct Hotkey(HotkeyModifiers Modifiers, uint VirtualKey)
{
    public const uint VkF13 = 0x7C;
    public const uint VkF24 = 0x87;

    public static Hotkey None => default;

    /// <summary>No key defined (the entry is then only reachable from the tray menu).</summary>
    public bool IsEmpty => VirtualKey == 0;

    /// <summary>
    /// Keys that can be used without any modifier. F13-F24 do not exist on most keyboards
    /// (they are usually produced by programmable keys), so they never conflict with typing.
    /// </summary>
    public bool CanBeUsedAlone => VirtualKey is >= VkF13 and <= VkF24;

    /// <summary>True when the hotkey can be registered: a key, plus a modifier unless the key may be used alone.</summary>
    public bool IsComplete => !IsEmpty && (Modifiers != HotkeyModifiers.None || CanBeUsedAlone);

    public override string ToString() => KeyNames.Format(this);
}
