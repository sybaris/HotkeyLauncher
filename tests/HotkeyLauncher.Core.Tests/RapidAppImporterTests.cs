using System.Text;
using HotkeyLauncher.Core.Import;
using HotkeyLauncher.Core.Models;

namespace HotkeyLauncher.Core.Tests;

public class RapidAppImporterTests
{
    // Same layout as the files written by RapidApp 1.5 (THotKeySetupList.WriteIniFile)
    private const string SampleIni = """
        [0]
        Menu=Outils
        SubMenu=Bloc-notes
        PathAndExeName=C:\Windows\notepad.exe
        Parameters=
        WorkingDirectory=
        WindowState=2
        Shift=0
        Ctrl=1
        Alt=1
        Win=0
        VKCode=78
        [1]
        Menu=Développement
        SubMenu=Éditeur
        PathAndExeName=C:\Program Files\Editor\editor.exe
        Parameters=-n "fichier.txt"
        WorkingDirectory=C:\Temp
        WindowState=1
        Shift=1
        Ctrl=0
        Alt=0
        Win=1
        VKCode=69
        [2]
        Menu=Outils
        SubMenu=Sans touche
        PathAndExeName=C:\Windows\explorer.exe
        WindowState=0
        Shift=0
        Ctrl=0
        Alt=0
        Win=0
        VKCode=65
        [MENULIST]
        Développement
        Outils
        """;

    [Fact]
    public void Parse_reads_groups_in_menulist_order()
    {
        var result = RapidAppImporter.Parse(SampleIni);

        Assert.Equal(["Développement", "Outils"], result.Groups);
    }

    [Fact]
    public void Parse_maps_every_field()
    {
        var result = RapidAppImporter.Parse(SampleIni);

        Assert.Equal(3, result.Entries.Count);
        var notepad = result.Entries[0];
        Assert.Equal("Bloc-notes", notepad.Name);
        Assert.Equal("Outils", notepad.Group);
        Assert.Equal(@"C:\Windows\notepad.exe", notepad.Target);
        Assert.Equal(LaunchWindowState.Maximized, notepad.WindowState);
        Assert.Equal(new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 78), notepad.Hotkey);
        Assert.True(notepad.IsEnabled);

        var editor = result.Entries[1];
        Assert.Equal("-n \"fichier.txt\"", editor.Arguments);
        Assert.Equal(@"C:\Temp", editor.WorkingDirectory);
        Assert.Equal(LaunchWindowState.Minimized, editor.WindowState);
        Assert.Equal(HotkeyModifiers.Shift | HotkeyModifiers.Win, editor.Modifiers);
    }

    [Fact]
    public void Parse_disables_hotkey_without_modifier()
    {
        var entry = RapidAppImporter.Parse(SampleIni).Entries[2];

        Assert.False(entry.IsEnabled);
        Assert.Equal(65u, entry.VirtualKey);
    }

    [Fact]
    public void Parse_skips_sections_without_target()
    {
        var result = RapidAppImporter.Parse("[0]\nMenu=A\nSubMenu=B\nVKCode=65\n");

        Assert.Empty(result.Entries);
        Assert.Equal(["0"], result.SkippedSections);
    }

    [Fact]
    public void Import_decodes_windows_1252_files()
    {
        var path = Path.GetTempFileName();
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            File.WriteAllBytes(path, Encoding.GetEncoding(1252).GetBytes(SampleIni.Replace("\n", "\r\n")));

            var result = RapidAppImporter.Import(path);

            Assert.Contains("Développement", result.Groups);
            Assert.Equal("Éditeur", result.Entries[1].Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Merge_disables_conflicting_hotkeys_and_keeps_existing_entries()
    {
        var settings = new AppSettings();
        settings.Hotkeys.Add(new HotkeyEntry { Name = "Existing", Target = "x", Hotkey = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 78) });

        var merge = SettingsMerger.Merge(settings, RapidAppImporter.Parse(SampleIni), replace: false);

        Assert.Equal(3, merge.Added);
        Assert.Equal(1, merge.DisabledBecauseOfConflict);
        Assert.Equal(4, settings.Hotkeys.Count);
        Assert.False(settings.Hotkeys.Single(h => h.Name == "Bloc-notes").IsEnabled);
    }

    [Fact]
    public void Merge_with_replace_removes_existing_entries()
    {
        var settings = new AppSettings();
        settings.Hotkeys.Add(new HotkeyEntry { Name = "Existing", Target = "x" });

        SettingsMerger.Merge(settings, RapidAppImporter.Parse(SampleIni), replace: true);

        Assert.DoesNotContain(settings.Hotkeys, h => h.Name == "Existing");
        Assert.Equal(["Développement", "Outils"], settings.Groups);
    }
}
