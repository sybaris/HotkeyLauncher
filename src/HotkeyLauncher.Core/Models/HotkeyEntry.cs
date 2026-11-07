using System.Text.Json.Serialization;

namespace HotkeyLauncher.Core.Models;

public enum LaunchWindowState
{
    Normal,
    Minimized,
    Maximized,
}

/// <summary>What to do when the program is already running.</summary>
public enum RunningBehavior
{
    /// <summary>Always start a new instance (RapidApp behavior).</summary>
    LaunchNew,
    /// <summary>Bring the existing window to the front, start the program only if it is not running.</summary>
    BringToFront,
}

/// <summary>Where to put the window of the launched program.</summary>
public enum WindowPlacement
{
    /// <summary>Let the program decide.</summary>
    Default,
    Center,
    LeftHalf,
    RightHalf,
    TopHalf,
    BottomHalf,
}

/// <summary>One configured shortcut: what to launch, how, and with which key combination.</summary>
public sealed class HotkeyEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name (the "sub-menu" of RapidApp).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional group (the "menu" of RapidApp). Empty = no group.</summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>Application, script, document, folder or URL. Environment variables (%USERPROFILE%...) are allowed.</summary>
    public string Target { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public LaunchWindowState WindowState { get; set; } = LaunchWindowState.Normal;

    public bool RunAsAdministrator { get; set; }

    /// <summary>Starts the program without showing its window (useful for scripts).</summary>
    public bool RunHidden { get; set; }

    public RunningBehavior IfRunning { get; set; } = RunningBehavior.LaunchNew;

    public WindowPlacement Placement { get; set; } = WindowPlacement.Default;

    /// <summary>Screen of the window: 0 = let Windows decide, 1 = main screen, 2+ = other screens.</summary>
    public int Monitor { get; set; }

    public HotkeyModifiers Modifiers { get; set; }

    public uint VirtualKey { get; set; }

    /// <summary>When false the hotkey is not registered (the entry stays available in the menus).</summary>
    public bool IsEnabled { get; set; } = true;

    // ---- Usage statistics ----

    public int LaunchCount { get; set; }

    public DateTime? LastLaunchedUtc { get; set; }

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

    /// <summary>True when the launched window must be moved after start.</summary>
    [JsonIgnore]
    public bool NeedsPlacement => Placement != WindowPlacement.Default || Monitor > 0;

    public HotkeyEntry Clone() => (HotkeyEntry)MemberwiseClone();

    public void ResetStatistics()
    {
        LaunchCount = 0;
        LastLaunchedUtc = null;
    }
}
