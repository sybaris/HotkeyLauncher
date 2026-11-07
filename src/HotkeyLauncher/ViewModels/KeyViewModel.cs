using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using HotkeyLauncher.Core;
using HotkeyLauncher.Core.Models;

namespace HotkeyLauncher.ViewModels;

public enum KeyState
{
    /// <summary>Selectable and free with the current modifiers.</summary>
    Free,
    /// <summary>The key of the hotkey being edited.</summary>
    Selected,
    /// <summary>Already used by another HotkeyLauncher shortcut.</summary>
    UsedByShortcut,
    /// <summary>Used by Windows or by another program.</summary>
    Taken,
    /// <summary>No modifier selected yet: availability unknown.</summary>
    Neutral,
    ModifierOff,
    ModifierOn,
    Decorative,
    Spacer,
}

/// <summary>One key of the on-screen keyboard.</summary>
public sealed partial class KeyViewModel : ObservableObject
{
    /// <summary>Size of one key unit, in pixels.</summary>
    public const double UnitSize = 38;

    public KeyViewModel(KeyDefinition definition)
    {
        Definition = definition;
        VirtualKey = definition.ResolveVirtualKey();
        Kind = definition.Kind == KeyKind.Normal && VirtualKey == 0 ? KeyKind.Decorative : definition.Kind;
        // Each key has a 2 px margin on both sides: the total space taken is exactly Width units
        Width = definition.Width * UnitSize - 4;
        State = Kind switch
        {
            KeyKind.Modifier => KeyState.ModifierOff,
            KeyKind.Decorative => KeyState.Decorative,
            KeyKind.Spacer => KeyState.Spacer,
            _ => KeyState.Neutral,
        };
        RefreshLabel();
    }

    public KeyDefinition Definition { get; }

    public KeyKind Kind { get; }

    public uint VirtualKey { get; private set; }

    /// <summary>Re-reads the key for the current Windows keyboard layout (AZERTY, QWERTY...).</summary>
    public void RefreshLayout()
    {
        if (Kind != KeyKind.Normal) return;
        VirtualKey = Definition.ResolveVirtualKey();
        RefreshLabel();
    }

    public HotkeyModifiers Modifier => Definition.Modifier;

    public double Width { get; }

    public static double Height => UnitSize - 4;

    public bool IsClickable => Kind is KeyKind.Normal or KeyKind.Modifier;

    [ObservableProperty]
    public partial string Label { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial KeyState State { get; set; }

    [ObservableProperty]
    public partial string? ToolTip { get; set; }

    /// <summary>Icon of the shortcut using this key (state <see cref="KeyState.UsedByShortcut"/>).</summary>
    [ObservableProperty]
    public partial ImageSource? OwnerIcon { get; set; }

    public void RefreshLabel()
    {
        Label = Kind switch
        {
            KeyKind.Modifier => KeyNames.ModifierNames(Modifier).First(),
            KeyKind.Normal => KeyNames.GetKeyName(VirtualKey),
            _ => Definition.Label ?? string.Empty,
        };
    }
}
