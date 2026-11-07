using System.Text.Json;
using HotkeyLauncher.Core.Models;
using HotkeyLauncher.Core.Settings;

namespace HotkeyLauncher.Core.Import;

/// <summary>Shortcuts read from a file, ready to be merged into the settings.</summary>
public sealed record ImportResult(IReadOnlyList<string> Groups, IReadOnlyList<HotkeyEntry> Entries, IReadOnlyList<string> SkippedSections);

/// <summary>
/// Export / import of the shortcuts (to copy them to another computer or share them).
/// The export contains the groups and shortcuts only, without the usage statistics nor the local preferences.
/// A full settings file can be imported as well.
/// </summary>
public static class SettingsTransfer
{
    public const string Format = "HotkeyLauncher.Export";
    public const string FileExtension = ".hotkeys.json";

    public static void Export(AppSettings settings, string path)
    {
        var file = new ExportFile
        {
            Format = Format,
            Version = 1,
            ExportedAt = DateTime.Now,
            Groups = [.. settings.Groups],
            Hotkeys = [.. settings.Hotkeys.Select(h =>
            {
                var copy = h.Clone();
                copy.ResetStatistics();
                return copy;
            })],
        };
        File.WriteAllText(path, JsonSerializer.Serialize(file, SettingsStore.JsonOptions));
    }

    /// <exception cref="InvalidDataException">The file does not contain HotkeyLauncher shortcuts.</exception>
    public static ImportResult Import(string path)
    {
        ExportFile? file;
        try
        {
            file = JsonSerializer.Deserialize<ExportFile>(File.ReadAllText(path), SettingsStore.JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }

        if (file?.Hotkeys == null) throw new InvalidDataException("No HotkeyLauncher shortcut in this file.");

        var entries = file.Hotkeys.Where(h => !string.IsNullOrWhiteSpace(h.Target)).ToList();
        foreach (var entry in entries)
        {
            entry.ResetStatistics();
            if (string.IsNullOrWhiteSpace(entry.Name)) entry.Name = Path.GetFileNameWithoutExtension(entry.Target.Trim());
        }

        var groups = (file.Groups ?? []).Concat(entries.Select(e => e.Group)).Where(g => !string.IsNullOrWhiteSpace(g)).Distinct().ToList();
        return new ImportResult(groups, entries, []);
    }

    private sealed class ExportFile
    {
        public string? Format { get; set; }
        public int Version { get; set; }
        public DateTime? ExportedAt { get; set; }
        public List<string>? Groups { get; set; }
        public List<HotkeyEntry>? Hotkeys { get; set; }
    }
}
