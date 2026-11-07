using HotkeyLauncher.Core.Models;

namespace HotkeyLauncher.Core.Import;

public sealed record MergeResult(int Added, int DisabledBecauseOfConflict);

public static class SettingsMerger
{
    /// <summary>
    /// Adds imported entries to the settings (or replaces them). An imported hotkey already used by
    /// another enabled entry is kept but disabled, so nothing is silently lost.
    /// </summary>
    public static MergeResult Merge(AppSettings settings, ImportResult import, bool replace)
    {
        if (replace)
        {
            settings.Hotkeys.Clear();
            settings.Groups.Clear();
        }

        foreach (var group in import.Groups.Where(g => !settings.Groups.Contains(g, StringComparer.CurrentCultureIgnoreCase)))
        {
            settings.Groups.Add(group);
        }

        var conflicts = 0;
        foreach (var imported in import.Entries)
        {
            var entry = imported.Clone();
            entry.Id = Guid.NewGuid();
            if (entry.IsEnabled && settings.Hotkeys.Any(h => h.IsEnabled && h.Hotkey == entry.Hotkey))
            {
                entry.IsEnabled = false;
                conflicts++;
            }

            settings.Hotkeys.Add(entry);
        }

        settings.Normalize();
        return new MergeResult(import.Entries.Count, conflicts);
    }
}
