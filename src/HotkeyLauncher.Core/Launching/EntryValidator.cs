using HotkeyLauncher.Core.Models;

namespace HotkeyLauncher.Core.Launching;

public enum ValidationIssue
{
    // Errors: the entry cannot be saved
    NameMissing,
    TargetMissing,
    HotkeyNeedsModifier,
    HotkeyAlreadyUsed,

    // Warnings: the entry can be saved but may not work
    TargetNotFound,
    WorkingDirectoryNotFound,
}

public static class EntryValidator
{
    public static bool IsError(ValidationIssue issue) => issue < ValidationIssue.TargetNotFound;

    /// <param name="entry">Entry to check.</param>
    /// <param name="others">The other entries (the edited one excluded) to detect duplicate hotkeys.</param>
    public static IReadOnlyList<ValidationIssue> Validate(HotkeyEntry entry, IEnumerable<HotkeyEntry> others)
    {
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(entry.Name)) issues.Add(ValidationIssue.NameMissing);

        var kind = TargetResolver.Classify(entry.Target);
        if (kind == TargetKind.Empty) issues.Add(ValidationIssue.TargetMissing);
        if (kind == TargetKind.NotFound) issues.Add(ValidationIssue.TargetNotFound);

        if (!string.IsNullOrWhiteSpace(entry.WorkingDirectory) && !Directory.Exists(TargetResolver.Expand(entry.WorkingDirectory)))
        {
            issues.Add(ValidationIssue.WorkingDirectoryNotFound);
        }

        if (!entry.Hotkey.IsEmpty)
        {
            if (!entry.Hotkey.IsComplete) issues.Add(ValidationIssue.HotkeyNeedsModifier);
            if (entry.IsEnabled && FindOwner(entry.Hotkey, others) != null) issues.Add(ValidationIssue.HotkeyAlreadyUsed);
        }

        return issues;
    }

    /// <summary>The enabled entry that already uses this hotkey, if any.</summary>
    public static HotkeyEntry? FindOwner(Hotkey hotkey, IEnumerable<HotkeyEntry> others) =>
        hotkey.IsEmpty ? null : others.FirstOrDefault(o => o.IsEnabled && o.Hotkey == hotkey);
}
