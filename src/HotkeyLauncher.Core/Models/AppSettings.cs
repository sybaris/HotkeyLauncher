namespace HotkeyLauncher.Core.Models;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>Everything persisted in the settings file.</summary>
public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>UI language ("en", "fr"...). Empty = follow Windows.</summary>
    public string Language { get; set; } = string.Empty;

    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>Display order of the groups.</summary>
    public List<string> Groups { get; set; } = [];

    public List<HotkeyEntry> Hotkeys { get; set; } = [];

    /// <summary>The "still running in the notification area" tip has already been shown.</summary>
    public bool TrayTipShown { get; set; }

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
