using System.Windows.Input;
using HotkeyLauncher.Core.Models;

namespace HotkeyLauncher.Views;

public enum CaptureAction
{
    /// <summary>A modifier alone: wait for the key.</summary>
    Ignore,
    /// <summary>A combination was typed.</summary>
    Capture,
    /// <summary>Backspace / Delete alone: remove the combination.</summary>
    Clear,
    /// <summary>Escape alone: leave the capture box.</summary>
    Leave,
    /// <summary>Tab alone: normal keyboard navigation.</summary>
    PassThrough,
}

/// <summary>Translates a WPF key press into a hotkey (shared by the capture boxes).</summary>
public static class KeyCapture
{
    private static readonly Key[] ModifierKeys =
    [
        Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin,
        Key.ImeProcessed, Key.DeadCharProcessed, Key.None,
    ];

    public static CaptureAction Read(KeyEventArgs e, out HotkeyModifiers modifiers, out uint virtualKey)
    {
        var key = e.Key switch
        {
            Key.System => e.SystemKey,         // keys pressed with Alt
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };

        modifiers = HotkeyModifiers.None;
        virtualKey = 0;
        var pressed = Keyboard.Modifiers;
        if (pressed.HasFlag(System.Windows.Input.ModifierKeys.Control)) modifiers |= HotkeyModifiers.Control;
        if (pressed.HasFlag(System.Windows.Input.ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (pressed.HasFlag(System.Windows.Input.ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (pressed.HasFlag(System.Windows.Input.ModifierKeys.Windows) || Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) modifiers |= HotkeyModifiers.Win;

        if (modifiers == HotkeyModifiers.None)
        {
            if (key == Key.Tab) return CaptureAction.PassThrough;
            if (key == Key.Escape) return CaptureAction.Leave;
            if (key is Key.Back or Key.Delete) return CaptureAction.Clear;
        }

        if (ModifierKeys.Contains(key)) return CaptureAction.Ignore;

        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        return CaptureAction.Capture;
    }
}
