using System.Text;
using HotkeyLauncher.Core.Models;

namespace HotkeyLauncher.Core.Import;

public sealed record RapidAppImportResult(IReadOnlyList<string> Groups, IReadOnlyList<HotkeyEntry> Entries, IReadOnlyList<string> SkippedSections);

/// <summary>
/// Reads the RapidApp.ini file of RapidApp (the Delphi ancestor of HotkeyLauncher).
/// Format: a [MENULIST] section with one menu name per line, then one numbered section per hotkey:
/// Menu, SubMenu, PathAndExeName, Parameters, WorkingDirectory, WindowState (0 normal, 1 minimized, 2 maximized),
/// Shift, Ctrl, Alt, Win (0/1) and VKCode.
/// </summary>
public static class RapidAppImporter
{
    public const string DefaultFileName = "RapidApp.ini";

    public static RapidAppImportResult Import(string path) => Parse(ReadText(path));

    public static RapidAppImportResult Parse(string content)
    {
        var ini = IniDocument.Parse(content);
        var groups = new List<string>();
        var entries = new List<HotkeyEntry>();
        var skipped = new List<string>();

        foreach (var line in ini.RawLines("MENULIST"))
        {
            var group = line.Trim();
            if (group.Length > 0 && !groups.Contains(group)) groups.Add(group);
        }

        foreach (var section in ini.Sections.Where(s => !s.Equals("MENULIST", StringComparison.OrdinalIgnoreCase)))
        {
            var target = ini.Get(section, "PathAndExeName");
            var vk = ParseInt(ini.Get(section, "VKCode"));
            if (string.IsNullOrWhiteSpace(target))
            {
                skipped.Add(section);
                continue;
            }

            var modifiers = HotkeyModifiers.None;
            if (ParseBool(ini.Get(section, "Ctrl"))) modifiers |= HotkeyModifiers.Control;
            if (ParseBool(ini.Get(section, "Alt"))) modifiers |= HotkeyModifiers.Alt;
            if (ParseBool(ini.Get(section, "Shift"))) modifiers |= HotkeyModifiers.Shift;
            if (ParseBool(ini.Get(section, "Win"))) modifiers |= HotkeyModifiers.Win;

            var name = ini.Get(section, "SubMenu").Trim();
            var entry = new HotkeyEntry
            {
                Name = name.Length > 0 ? name : Path.GetFileNameWithoutExtension(target.Trim()),
                Group = ini.Get(section, "Menu").Trim(),
                Target = target.Trim(),
                Arguments = ini.Get(section, "Parameters").Trim(),
                WorkingDirectory = ini.Get(section, "WorkingDirectory").Trim(),
                WindowState = ParseInt(ini.Get(section, "WindowState")) switch
                {
                    1 => LaunchWindowState.Minimized,
                    2 => LaunchWindowState.Maximized,
                    _ => LaunchWindowState.Normal,
                },
                Modifiers = modifiers,
                VirtualKey = vk is > 0 and < 256 ? (uint)vk : 0,
            };
            entry.IsEnabled = entry.Hotkey.IsComplete;
            entries.Add(entry);

            if (entry.Group.Length > 0 && !groups.Contains(entry.Group)) groups.Add(entry.Group);
        }

        return new RapidAppImportResult(groups, entries, skipped);
    }

    /// <summary>RapidApp was a Delphi 7 (ANSI) program: the file is Windows-1252 unless it has a BOM.</summary>
    internal static string ReadText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252).GetString(bytes);
    }

    private static int ParseInt(string value) => int.TryParse(value.Trim(), out var i) ? i : 0;

    // Delphi TIniFile.WriteBool writes 1/0; also accept true/false
    private static bool ParseBool(string value) =>
        value.Trim() is var v && (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) || (int.TryParse(v, out var i) && i != 0));
}
