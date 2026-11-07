using HotkeyLauncher.Core.Models;

namespace HotkeyLauncher.Core;

/// <summary>
/// Shortcuts handled by Windows itself. Many of them are not visible through RegisterHotKey
/// (the shell intercepts them earlier), so they are listed explicitly.
/// </summary>
public static class ReservedHotkeys
{
    private const HotkeyModifiers Win = HotkeyModifiers.Win;
    private const HotkeyModifiers WinShift = HotkeyModifiers.Win | HotkeyModifiers.Shift;
    private const HotkeyModifiers WinCtrl = HotkeyModifiers.Win | HotkeyModifiers.Control;
    private const HotkeyModifiers CtrlAlt = HotkeyModifiers.Control | HotkeyModifiers.Alt;
    private const HotkeyModifiers CtrlShift = HotkeyModifiers.Control | HotkeyModifiers.Shift;

    private static readonly HashSet<Hotkey> Reserved = Build();

    public static bool IsReserved(Hotkey hotkey) => Reserved.Contains(hotkey);

    private static HashSet<Hotkey> Build()
    {
        var set = new HashSet<Hotkey>();

        // Win + letter (Action center, Explorer, Desktop, Settings, Lock, Run, Search, Clipboard, Quick link menu...)
        foreach (var c in "ABCDEGHIJKLMNOPQRSTUVWXYZ") set.Add(new Hotkey(Win, c));
        // Win + digit: taskbar programs (also with Shift / Ctrl)
        for (uint d = '0'; d <= '9'; d++)
        {
            set.Add(new Hotkey(Win, d));
            set.Add(new Hotkey(WinShift, d));
            set.Add(new Hotkey(WinCtrl, d));
        }

        // Win + navigation / special keys
        uint[] winKeys = [0x09, 0x20, 0x25, 0x26, 0x27, 0x28, 0x24, 0x2C, 0x13, 0xBB, 0xBD, 0xBC, 0xBE, 0x1B, 0x0D];
        foreach (var vk in winKeys) set.Add(new Hotkey(Win, vk));

        // Win + Shift: S (screenshot), M, arrows, C...
        foreach (var c in "SMCTVR") set.Add(new Hotkey(WinShift, c));
        foreach (uint vk in new uint[] { 0x25, 0x26, 0x27, 0x28 }) set.Add(new Hotkey(WinShift, vk));

        // Win + Ctrl: virtual desktops, narrator, search...
        foreach (var c in "DFCNOQS") set.Add(new Hotkey(WinCtrl, c));
        foreach (uint vk in new uint[] { 0x25, 0x27, 0x73, 0x0D }) set.Add(new Hotkey(WinCtrl, vk));

        // Classics
        set.Add(new Hotkey(CtrlAlt, 0x2E));          // Ctrl+Alt+Del
        set.Add(new Hotkey(CtrlShift, 0x1B));        // Ctrl+Shift+Esc
        set.Add(new Hotkey(HotkeyModifiers.Alt, 0x73)); // Alt+F4
        set.Add(new Hotkey(HotkeyModifiers.Alt, 0x09)); // Alt+Tab
        set.Add(new Hotkey(HotkeyModifiers.Control, 0x1B)); // Ctrl+Esc

        return set;
    }
}
