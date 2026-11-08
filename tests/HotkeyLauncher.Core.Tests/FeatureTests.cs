using HotkeyLauncher.Core.Import;
using HotkeyLauncher.Core.Launching;
using HotkeyLauncher.Core.Models;
using HotkeyLauncher.Core.Settings;

namespace HotkeyLauncher.Core.Tests;

public class QuickSearchTests
{
    private static readonly HotkeyEntry[] Entries =
    [
        new() { Name = "Visual Studio Code", Group = "Dev", Target = @"C:\code.exe", LaunchCount = 3 },
        new() { Name = "Bloc-notes", Group = "Outils", Target = @"C:\Windows\notepad.exe", LaunchCount = 10 },
        new() { Name = "Éditeur de registre", Group = "Outils", Target = "regedit" },
        new() { Name = "GitHub", Target = "https://github.com", LastLaunchedUtc = DateTime.UtcNow },
    ];

    [Fact]
    public void Prefix_of_name_ranks_first() =>
        Assert.Equal("Bloc-notes", QuickSearch.Search(Entries, "blo", 5)[0].Name);

    [Fact]
    public void Initials_are_matched() =>
        Assert.Equal("Visual Studio Code", QuickSearch.Search(Entries, "vsc", 5)[0].Name);

    [Fact]
    public void Search_ignores_accents_and_case() =>
        Assert.Equal("Éditeur de registre", QuickSearch.Search(Entries, "EDITEUR", 5)[0].Name);

    [Fact]
    public void Group_and_target_are_searched_too()
    {
        Assert.Equal(2, QuickSearch.Search(Entries, "outils", 5).Count);
        Assert.Equal("Bloc-notes", QuickSearch.Search(Entries, "notepad", 5)[0].Name);
    }

    [Fact]
    public void Empty_query_returns_recent_then_most_used()
    {
        var results = QuickSearch.Search(Entries, "", 2);
        Assert.Equal(["GitHub", "Bloc-notes"], results.Select(r => r.Name));
    }

    [Fact]
    public void No_match_returns_nothing() => Assert.Empty(QuickSearch.Search(Entries, "zzz", 5));
}

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v1.2.0", "1.1.9", true)]
    [InlineData("v1.2.0", "1.2.0", false)]
    [InlineData("1.10.0", "1.9.0", true)]
    [InlineData("v2.0.0-beta.1", "1.5.0", true)]
    [InlineData("v1.0", "1.0.0", false)]
    [InlineData("not-a-version", "1.0.0", false)]
    public void IsNewer(string tag, string current, bool expected) =>
        Assert.Equal(expected, UpdateChecker.IsNewer(tag, Version.Parse(current), out _));
}

public class TransferAndBackupTests
{
    [Fact]
    public void Export_then_import_round_trips_without_statistics()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hl-export-{Guid.NewGuid():N}.hotkeys.json");
        try
        {
            var settings = new AppSettings();
            settings.Hotkeys.Add(new HotkeyEntry
            {
                Name = "Bloc-notes",
                Group = "Outils",
                Target = "notepad",
                Placement = WindowPlacement.LeftHalf,
                Monitor = 2,
                IfRunning = RunningBehavior.BringToFront,
                RunHidden = true,
                LaunchCount = 42,
                LastLaunchedUtc = DateTime.UtcNow,
                Hotkey = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4E),
            });
            settings.Normalize();

            SettingsTransfer.Export(settings, path);
            var import = SettingsTransfer.Import(path);

            var entry = Assert.Single(import.Entries);
            Assert.Equal(["Outils"], import.Groups);
            Assert.Equal(WindowPlacement.LeftHalf, entry.Placement);
            Assert.Equal(2, entry.Monitor);
            Assert.Equal(RunningBehavior.BringToFront, entry.IfRunning);
            Assert.True(entry.RunHidden);
            Assert.Equal(0, entry.LaunchCount);
            Assert.Null(entry.LastLaunchedUtc);
            Assert.Equal(42, settings.Hotkeys[0].LaunchCount); // the exported settings are not modified
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_rejects_a_foreign_json_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hl-foreign-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{ "name": "something else" }""");
            Assert.Throws<InvalidDataException>(() => SettingsTransfer.Import(path));
            File.WriteAllText(path, "not json");
            Assert.Throws<InvalidDataException>(() => SettingsTransfer.Import(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_full_settings_file_can_be_imported()
    {
        var directory = Directory.CreateTempSubdirectory("hl-settings-").FullName;
        try
        {
            var store = new SettingsStore(Path.Combine(directory, SettingsStore.FileName));
            var settings = new AppSettings();
            settings.Hotkeys.Add(new HotkeyEntry { Name = "A", Target = "notepad" });
            store.Save(settings);

            Assert.Single(SettingsTransfer.Import(store.FilePath).Entries);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_backs_up_the_previous_file_once_per_session()
    {
        var directory = Directory.CreateTempSubdirectory("hl-backup-").FullName;
        try
        {
            var path = Path.Combine(directory, SettingsStore.FileName);
            new SettingsStore(path).Save(new AppSettings()); // first file, nothing to back up

            var store = new SettingsStore(path);
            store.Save(new AppSettings());
            store.Save(new AppSettings());

            var store2 = new SettingsStore(path);
            Assert.Single(Directory.GetFiles(store.BackupDirectory));
            store2.Save(new AppSettings());
            Assert.InRange(Directory.GetFiles(store.BackupDirectory).Length, 1, SettingsStore.BackupCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Backups_are_limited()
    {
        var directory = Directory.CreateTempSubdirectory("hl-backup-limit-").FullName;
        try
        {
            var path = Path.Combine(directory, SettingsStore.FileName);
            new SettingsStore(path).Save(new AppSettings());
            var backups = Path.Combine(directory, "backups");
            Directory.CreateDirectory(backups);
            for (var i = 0; i < 5; i++) File.WriteAllText(Path.Combine(backups, $"settings-2020010{i}-000000.json"), "{}");

            new SettingsStore(path).Save(new AppSettings());

            Assert.Equal(SettingsStore.BackupCount, Directory.GetFiles(backups).Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

public class LaunchOptionsTests
{
    [Fact]
    public void PowerShell_scripts_are_run_by_PowerShell()
    {
        var script = Path.Combine(Path.GetTempPath(), $"hl-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(script, "Write-Host test");
        try
        {
            var psi = Launcher.BuildStartInfo(new HotkeyEntry { Target = script, Arguments = "-Name x", RunHidden = true });

            Assert.Equal(TargetKind.Script, TargetResolver.Classify(script));
            Assert.Equal("powershell.exe", psi.FileName);
            Assert.Contains($"-File \"{script}\" -Name x", psi.Arguments);
            Assert.Contains("-WindowStyle Hidden", psi.Arguments);
            Assert.Equal(System.Diagnostics.ProcessWindowStyle.Hidden, psi.WindowStyle);
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Fact]
    public void Bring_to_front_is_only_for_programs()
    {
        Assert.True(Launcher.CanBringToFront(@"%windir%\notepad.exe"));
        Assert.True(Launcher.CanBringToFront("notepad"));
        Assert.False(Launcher.CanBringToFront("https://github.com"));
        Assert.False(Launcher.CanBringToFront(@"%windir%"));
    }

    [Theory]
    [InlineData(WindowPlacement.LeftHalf, 0, 0, 960, 1040)]
    [InlineData(WindowPlacement.RightHalf, 960, 0, 960, 1040)]
    [InlineData(WindowPlacement.TopHalf, 0, 0, 1920, 520)]
    [InlineData(WindowPlacement.BottomHalf, 0, 520, 1920, 520)]
    [InlineData(WindowPlacement.Center, 560, 270, 800, 500)]
    public void Placement_bounds(WindowPlacement placement, int x, int y, int width, int height) =>
        Assert.Equal((x, y, width, height), Screens.ComputeBounds(placement, (0, 0, 1920, 1040), (800, 500)));

    [Fact]
    public void Centered_window_is_never_bigger_than_the_screen() =>
        Assert.Equal((0, 0, 1920, 1040), Screens.ComputeBounds(WindowPlacement.Center, (0, 0, 1920, 1040), (3000, 2000)));
}

public class KeyboardLayoutTests
{
    [Theory]
    [InlineData(KeyboardShape.Iso)]
    [InlineData(KeyboardShape.Ansi)]
    public void Every_row_of_the_main_block_has_the_same_width(KeyboardShape shape)
    {
        var widths = KeyboardLayout.MainBlock(shape).Skip(1).Select(row => row.Sum(k => k.Width)).Distinct();
        Assert.Equal([15.0], widths);
    }

    [Fact]
    public void Ansi_has_no_extra_key_next_to_left_shift()
    {
        Assert.Contains(KeyboardLayout.IsoMainBlock[4], k => k.ScanCode == 0x56);
        Assert.DoesNotContain(KeyboardLayout.AnsiMainBlock.SelectMany(r => r), k => k.ScanCode == 0x56);
    }

    [Fact]
    public void Numpad_rows_have_the_same_width() =>
        Assert.Equal([4.0], KeyboardLayout.NumpadBlock.Select(row => row.Sum(k => k.Width)).Distinct());

    [Fact]
    public void Numpad_keys_have_readable_names()
    {
        Assert.Equal("Num 7", KeyNames.GetKeyName(0x67));
        Assert.Equal("Num +", KeyNames.GetKeyName(0x6B));
    }
}

public class QuickLauncherDefaultTests
{
    [Fact]
    public void Default_hotkey_is_win_alt_space_and_first_candidate()
    {
        var settings = new AppSettings();
        Assert.Equal(new Hotkey(HotkeyModifiers.Win | HotkeyModifiers.Alt, 0x20), settings.QuickLauncherHotkey);
        Assert.Equal(AppSettings.QuickLauncherCandidates[0], settings.QuickLauncherHotkey);
        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
    }

    [Fact]
    public void Candidates_are_not_reserved_by_windows()
    {
        Assert.All(AppSettings.QuickLauncherCandidates, h => Assert.False(ReservedHotkeys.IsReserved(h)));
    }
}

public class DemoSettingsTests
{
    [Fact]
    public void Demo_settings_file_loads()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "HotkeyLauncher.slnx"))) dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);

        var settings = new SettingsStore(Path.Combine(dir, "docs", "screenshots", "demo.json")).Load();

        Assert.Equal(12, settings.Hotkeys.Count);
        Assert.Equal(AppSettings.QuickLauncherCandidates[0], settings.QuickLauncherHotkey);
        Assert.DoesNotContain(settings.Hotkeys, h => h.Target.Contains(@"\Users\", StringComparison.OrdinalIgnoreCase));
    }
}

public class PackageManagerLocationTests
{
    [Theory]
    [InlineData(@"C:\Users\x\AppData\Local\Microsoft\WinGet\Packages\Sybaris.HotkeyLauncher_Microsoft.Winget.Source_8wekyb3d8bbwe\", true)]
    [InlineData(@"C:\Program Files\WinGet\Packages\Sybaris.HotkeyLauncher_Microsoft.Winget.Source_8wekyb3d8bbwe", true)]
    [InlineData(@"C:\ProgramData\chocolatey\lib\hotkeylauncher\tools\", true)]
    [InlineData(@"C:\Users\x\scoop\apps\hotkeylauncher\current\", false)]
    [InlineData(@"C:\Tools\HotkeyLauncher\", false)]
    public void Package_manager_folders_are_detected(string directory, bool expected) =>
        Assert.Equal(expected, SettingsStore.IsManagedByPackageManager(directory));
}
