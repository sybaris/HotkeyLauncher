namespace HotkeyLauncher.Core.Models;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>Shape of the on-screen keyboard.</summary>
public enum KeyboardShape
{
    /// <summary>Deduced from the Windows keyboard layout.</summary>
    Auto,
    /// <summary>European keyboards (AZERTY, QWERTZ, UK...): extra key next to left Shift, tall Enter.</summary>
    Iso,
    /// <summary>US keyboards.</summary>
    Ansi,
}

/// <summary>Order of the shortcuts inside each group of the list.</summary>
public enum ListSortMode
{
    Custom,
    Name,
    MostUsed,
    RecentlyUsed,
}

/// <summary>Everything persisted in the settings file.</summary>
public sealed class AppSettings
{
    /// <summary>2: default quick launcher hotkey changed from Ctrl+Alt+Space to Win+Alt+Space.</summary>
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>UI language ("en", "fr"...). Empty = follow Windows.</summary>
    public string Language { get; set; } = string.Empty;

    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>Display order of the groups.</summary>
    public List<string> Groups { get; set; } = [];

    public List<HotkeyEntry> Hotkeys { get; set; } = [];

    /// <summary>The "still running in the notification area" tip has already been shown.</summary>
    public bool TrayTipShown { get; set; }

    // ---- Quick launcher (search box opened by a global hotkey) ----

    public bool QuickLauncherEnabled { get; set; } = true;

    /// <summary>
    /// Default quick launcher hotkeys, by order of preference: the first one not taken by another program is used.
    /// </summary>
    public static readonly IReadOnlyList<Hotkey> QuickLauncherCandidates =
    [
        new(HotkeyModifiers.Win | HotkeyModifiers.Alt, 0x20),
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x20),
        new(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x20),
    ];

    public HotkeyModifiers QuickLauncherModifiers { get; set; } = HotkeyModifiers.Win | HotkeyModifiers.Alt;

    /// <summary>Default: Space.</summary>
    public uint QuickLauncherKey { get; set; } = 0x20;

    [System.Text.Json.Serialization.JsonIgnore]
    public Hotkey QuickLauncherHotkey
    {
        get => new(QuickLauncherModifiers, QuickLauncherKey);
        set
        {
            QuickLauncherModifiers = value.Modifiers;
            QuickLauncherKey = value.VirtualKey;
        }
    }

    // ---- Display ----

    public KeyboardShape KeyboardShape { get; set; } = KeyboardShape.Auto;

    public bool ShowNumpad { get; set; }

    public ListSortMode SortMode { get; set; } = ListSortMode.Custom;

    // ---- Updates ----

    public bool CheckForUpdates { get; set; } = true;

    public DateTime? LastUpdateCheckUtc { get; set; }

    /// <summary>
    /// Makes the settings consistent: trims names, removes unknown/duplicate/empty groups, adds missing groups,
    /// and orders the entries group by group (stable inside a group). Entries without group come last.
    /// </summary>
    public void Normalize()
    {
        foreach (var entry in Hotkeys)
        {
            entry.Name = entry.Name.Trim();
            entry.Group = entry.Group.Trim();
            entry.Target = entry.Target.Trim();
        }

        var groups = new List<string>();
        foreach (var group in Groups.Select(g => g.Trim()).Concat(Hotkeys.Select(h => h.Group)))
        {
            if (group.Length > 0 && !groups.Contains(group, StringComparer.CurrentCultureIgnoreCase))
            {
                groups.Add(group);
            }
        }

        // Keep only groups that still contain at least one entry
        groups.RemoveAll(g => !Hotkeys.Any(h => string.Equals(h.Group, g, StringComparison.CurrentCultureIgnoreCase)));

        // Use the group spelling of the group list for every entry
        foreach (var entry in Hotkeys.Where(h => h.Group.Length > 0))
        {
            entry.Group = groups.First(g => string.Equals(g, entry.Group, StringComparison.CurrentCultureIgnoreCase));
        }

        Groups = groups;
        Hotkeys = [.. Hotkeys.Select((h, i) => (h, i)).OrderBy(x => GroupIndex(x.h.Group)).ThenBy(x => x.i).Select(x => x.h)];
    }

    /// <summary>Order index of a group; entries without group are placed after every group.</summary>
    public int GroupIndex(string group) => group.Length == 0 ? int.MaxValue : Groups.IndexOf(group) is var i and >= 0 ? i : int.MaxValue - 1;
}
