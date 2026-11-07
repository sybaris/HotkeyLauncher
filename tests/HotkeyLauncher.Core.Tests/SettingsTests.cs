using HotkeyLauncher.Core.Models;
using HotkeyLauncher.Core.Settings;

namespace HotkeyLauncher.Core.Tests;

public class SettingsTests
{
    [Fact]
    public void Save_then_load_round_trips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hl-test-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(path);
            var settings = new AppSettings { Language = "fr", Theme = AppTheme.Dark };
            settings.Hotkeys.Add(new HotkeyEntry
            {
                Name = "Bloc-notes",
                Group = "Outils",
                Target = @"%windir%\notepad.exe",
                RunAsAdministrator = true,
                WindowState = LaunchWindowState.Maximized,
                Hotkey = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4E),
            });

            store.Save(settings);
            var loaded = store.Load();

            Assert.Equal("fr", loaded.Language);
            Assert.Equal(AppTheme.Dark, loaded.Theme);
            Assert.Equal(["Outils"], loaded.Groups);
            var entry = Assert.Single(loaded.Hotkeys);
            Assert.Equal(settings.Hotkeys[0].Id, entry.Id);
            Assert.Equal(@"%windir%\notepad.exe", entry.Target);
            Assert.True(entry.RunAsAdministrator);
            Assert.Equal(LaunchWindowState.Maximized, entry.WindowState);
            Assert.Equal(new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4E), entry.Hotkey);

            // Human readable file: enums as text, accents not escaped
            var json = File.ReadAllText(path);
            Assert.Contains("\"Alt, Control\"", json);
            Assert.Contains("Bloc-notes", json);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_throws_a_clear_error_on_invalid_json()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hl-test-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ not json");
            Assert.Throws<SettingsLoadException>(() => new SettingsStore(path).Load());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Normalize_orders_entries_by_group_and_drops_empty_groups()
    {
        var settings = new AppSettings { Groups = ["B", "A", "Empty"] };
        settings.Hotkeys.Add(new HotkeyEntry { Name = "a1", Group = "a" });
        settings.Hotkeys.Add(new HotkeyEntry { Name = "none" });
        settings.Hotkeys.Add(new HotkeyEntry { Name = "b1", Group = "B" });
        settings.Hotkeys.Add(new HotkeyEntry { Name = "a2", Group = "A" });
        settings.Hotkeys.Add(new HotkeyEntry { Name = "c1", Group = "C" });

        settings.Normalize();

        Assert.Equal(["B", "A", "C"], settings.Groups);
        Assert.Equal(["b1", "a1", "a2", "c1", "none"], settings.Hotkeys.Select(h => h.Name));
        Assert.Equal("A", settings.Hotkeys[1].Group);
    }
}
