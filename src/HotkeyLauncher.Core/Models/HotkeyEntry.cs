using System.Text.Json.Serialization;

namespace HotkeyLauncher.Core.Models;

public enum LaunchWindowState
{
    Normal,
    Minimized,
    Maximized,
}

/// <summary>One configured shortcut: what to launch, how, and with which key combination.</summary>
public sealed class HotkeyEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name (the "sub-menu" of RapidApp).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional group (the "menu" of RapidApp). Empty = no group.</summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>Application, document, folder or URL. Environment variables (%USERPROFILE%...) are allowed.</summary>
    public string Target { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public LaunchWindowState WindowState { get; set; } = LaunchWindowState.Normal;

    public bool RunAsAdministrator { get; set; }

    public HotkeyModifiers Modifiers { get; set; }

    public uint VirtualKey { get; set; }

    /// <summary>When false the hotkey is not registered (the entry stays available in the tray menu).</summary>
    public bool IsEnabled { get; set; } = true;

    [JsonIgnore]
    public Hotkey Hotkey
    {
        get => new(Modifiers, VirtualKey);
        set
        {
            Modifiers = value.Modifiers;
            VirtualKey = value.VirtualKey;
        }
    }

    public HotkeyEntry Clone() => (HotkeyEntry)MemberwiseClone();
}
