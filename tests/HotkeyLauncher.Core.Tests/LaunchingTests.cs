using System.Diagnostics;
using HotkeyLauncher.Core.Launching;
using HotkeyLauncher.Core.Models;

namespace HotkeyLauncher.Core.Tests;

public class LaunchingTests
{
    private static readonly string Notepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe");

    [Theory]
    [InlineData("https://github.com", TargetKind.Url)]
    [InlineData("mailto:someone@example.com", TargetKind.Url)]
    [InlineData("ms-settings:display", TargetKind.Url)]
    [InlineData("", TargetKind.Empty)]
    [InlineData(@"C:\does\not\exist.exe", TargetKind.NotFound)]
    [InlineData(@"%windir%", TargetKind.Folder)]
    [InlineData(@"%windir%\notepad.exe", TargetKind.Application)]
    [InlineData(@"""%windir%\notepad.exe""", TargetKind.Application)]
    [InlineData("notepad", TargetKind.Command)]
    public void Classify(string target, TargetKind expected) => Assert.Equal(expected, TargetResolver.Classify(target));

    [Fact]
    public void Drive_paths_are_not_urls() => Assert.False(TargetResolver.IsUrl(@"C:\Windows"));

    [Fact]
    public void BuildStartInfo_maps_options()
    {
        var entry = new HotkeyEntry
        {
            Target = @"%windir%\notepad.exe",
            Arguments = "file.txt",
            WindowState = LaunchWindowState.Maximized,
            RunAsAdministrator = true,
        };

        var psi = Launcher.BuildStartInfo(entry);

        Assert.Equal(Notepad, psi.FileName, ignoreCase: true);
        Assert.Equal("file.txt", psi.Arguments);
        Assert.Equal(ProcessWindowStyle.Maximized, psi.WindowStyle);
        Assert.Equal("runas", psi.Verb);
        Assert.Equal(Path.GetDirectoryName(Notepad), psi.WorkingDirectory, ignoreCase: true);
        Assert.True(psi.UseShellExecute);
    }

    [Fact]
    public void BuildStartInfo_never_elevates_urls()
    {
        var psi = Launcher.BuildStartInfo(new HotkeyEntry { Target = "https://example.com", RunAsAdministrator = true });

        Assert.Equal(string.Empty, psi.Verb);
        Assert.Equal("https://example.com", psi.FileName);
    }

    [Fact]
    public void Validator_reports_errors_and_warnings()
    {
        var other = new HotkeyEntry { Name = "Other", Target = Notepad, Hotkey = new Hotkey(HotkeyModifiers.Control, 0x41) };
        var entry = new HotkeyEntry { Name = "", Target = @"C:\nope.exe", Hotkey = new Hotkey(HotkeyModifiers.Control, 0x41) };

        var issues = EntryValidator.Validate(entry, [other]);

        Assert.Contains(ValidationIssue.NameMissing, issues);
        Assert.Contains(ValidationIssue.TargetNotFound, issues);
        Assert.Contains(ValidationIssue.HotkeyAlreadyUsed, issues);
        Assert.True(EntryValidator.IsError(ValidationIssue.HotkeyAlreadyUsed));
        Assert.False(EntryValidator.IsError(ValidationIssue.TargetNotFound));
    }

    [Fact]
    public void Disabled_entries_do_not_conflict()
    {
        var other = new HotkeyEntry { Name = "Other", Target = Notepad, IsEnabled = false, Hotkey = new Hotkey(HotkeyModifiers.Control, 0x41) };
        var entry = new HotkeyEntry { Name = "Me", Target = Notepad, Hotkey = new Hotkey(HotkeyModifiers.Control, 0x41) };

        Assert.Empty(EntryValidator.Validate(entry, [other]));
    }

    [Theory]
    [InlineData(HotkeyModifiers.None, 0x41u, false)]
    [InlineData(HotkeyModifiers.Control, 0x41u, true)]
    [InlineData(HotkeyModifiers.None, 0x7Cu, true)]   // F13 alone
    [InlineData(HotkeyModifiers.None, 0x70u, false)]  // F1 alone
    [InlineData(HotkeyModifiers.Control, 0u, false)]
    public void Hotkey_completeness(HotkeyModifiers modifiers, uint vk, bool complete) =>
        Assert.Equal(complete, new Hotkey(modifiers, vk).IsComplete);

    [Fact]
    public void Hotkey_format()
    {
        Assert.Equal("Ctrl + Alt + N", new Hotkey(HotkeyModifiers.Alt | HotkeyModifiers.Control, 0x4E).ToString());
        Assert.Equal("Win + F5", new Hotkey(HotkeyModifiers.Win, 0x74).ToString());
    }
}
