using System.IO;
using HotkeyLauncher.Core;
using HotkeyLauncher.Core.Models;
using HotkeyLauncher.ViewModels;

namespace HotkeyLauncher.Tests;

public class EditorViewModelTests
{
    private const HotkeyModifiers CtrlAlt = HotkeyModifiers.Control | HotkeyModifiers.Alt;
    private const uint VkA = 0x41, VkB = 0x42, VkC = 0x43, VkN = 0x4E, VkT = 0x54;

    private static readonly string Notepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe");

    /// <summary>Other shortcut already using Ctrl+Alt+N.</summary>
    private static readonly HotkeyEntry Other = new() { Name = "Other", Target = Notepad, Hotkey = new Hotkey(CtrlAlt, VkN) };

    /// <param name="takenByOthers">Combinations reported as registered by "other programs".</param>
    private static EditorViewModel Create(HotkeyEntry? entry = null, params Hotkey[] takenByOthers) =>
        new(entry ?? new HotkeyEntry(), [Other], ["Tools"], h => !takenByOthers.Contains(h), isNew: entry == null);

    private static KeyViewModel Key(EditorViewModel vm, uint vk) =>
        vm.MainKeyboard.Concat(vm.NavigationKeyboard).SelectMany(r => r).First(k => k.VirtualKey == vk);

    [Fact]
    public void Free_combination_is_available_and_has_no_suggestion()
    {
        var vm = Create();
        vm.Capture(CtrlAlt, VkB);

        Assert.Equal(HotkeyStatus.Available, vm.HotkeyStatus);
        Assert.Empty(vm.Suggestions);
    }

    [Fact]
    public void Combination_of_another_shortcut_is_an_error()
    {
        var vm = Create(new HotkeyEntry { Name = "Me", Target = Notepad });
        vm.Capture(CtrlAlt, VkN);

        Assert.Equal(HotkeyStatus.UsedByShortcut, vm.HotkeyStatus);
        Assert.False(vm.CanSave);
        Assert.NotNull(vm.ErrorText);
    }

    [Fact]
    public void Combination_taken_by_another_program_is_only_a_warning()
    {
        var vm = Create(new HotkeyEntry { Name = "Me", Target = Notepad }, new Hotkey(CtrlAlt, VkC));
        vm.Capture(CtrlAlt, VkC);

        Assert.Equal(HotkeyStatus.Taken, vm.HotkeyStatus);
        Assert.True(vm.CanSave);
        Assert.NotNull(vm.WarningText);
    }

    [Fact]
    public void Windows_reserved_combination_is_detected() =>
        Assert.Equal(HotkeyStatus.ReservedByWindows, With(vm => vm.Capture(HotkeyModifiers.Win, 0x45)).HotkeyStatus); // Win+E

    [Fact]
    public void Key_alone_needs_a_modifier_but_F13_does_not()
    {
        var vm = Create();
        vm.Capture(HotkeyModifiers.None, VkA);
        Assert.Equal(HotkeyStatus.NeedsModifier, vm.HotkeyStatus);

        vm.Capture(HotkeyModifiers.None, Hotkey.VkF13);
        Assert.Equal(HotkeyStatus.Available, vm.HotkeyStatus);
    }

    [Fact]
    public void Capture_without_modifier_keeps_the_toggled_modifiers()
    {
        var vm = Create();
        vm.IsCtrl = true;
        vm.IsShift = true;
        vm.Capture(HotkeyModifiers.None, VkT);

        Assert.Equal(new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Shift, VkT), vm.Hotkey);
    }

    [Fact]
    public void Suggestions_skip_used_and_taken_combinations()
    {
        var vm = Create(null, new Hotkey(CtrlAlt, VkA));
        vm.Capture(CtrlAlt, VkN); // used by "Other": suggestions are proposed

        var suggested = vm.Suggestions.Select(s => s.Hotkey).ToList();
        Assert.NotEmpty(suggested);
        Assert.DoesNotContain(new Hotkey(CtrlAlt, VkA), suggested);
        Assert.DoesNotContain(new Hotkey(CtrlAlt, VkN), suggested);
        Assert.All(suggested, h => Assert.Equal(CtrlAlt, h.Modifiers));
    }

    [Fact]
    public void Keyboard_colors_follow_the_modifiers()
    {
        var vm = Create(null, new Hotkey(CtrlAlt, VkC));
        Assert.Equal(KeyState.Neutral, Key(vm, VkN).State); // no modifier yet: unknown

        vm.IsCtrl = true;
        vm.IsAlt = true;
        Assert.Equal(KeyState.UsedByShortcut, Key(vm, VkN).State);
        Assert.Equal(KeyState.Taken, Key(vm, VkC).State);
        Assert.Equal(KeyState.Free, Key(vm, VkB).State);

        vm.KeyClickedCommand.Execute(Key(vm, VkB));
        Assert.Equal(KeyState.Selected, Key(vm, VkB).State);
        Assert.Equal(new Hotkey(CtrlAlt, VkB), vm.Hotkey);
    }

    [Fact]
    public void Clicking_a_modifier_key_toggles_it()
    {
        var vm = Create();
        var ctrl = vm.MainKeyboard.SelectMany(r => r).First(k => k.Kind == KeyKind.Modifier && k.Modifier == HotkeyModifiers.Control);

        vm.KeyClickedCommand.Execute(ctrl);
        Assert.True(vm.IsCtrl);
        Assert.Equal(KeyState.ModifierOn, ctrl.State);

        vm.KeyClickedCommand.Execute(ctrl);
        Assert.False(vm.IsCtrl);
    }

    [Fact]
    public void Name_is_guessed_from_the_target_until_typed()
    {
        var vm = Create();
        vm.Target = "https://www.github.com/sybaris";
        Assert.Equal("github.com", vm.Name);

        vm.Name = "My repositories";
        vm.Target = "https://example.com";
        Assert.Equal("My repositories", vm.Name);
    }

    [Fact]
    public void Name_and_target_are_required()
    {
        var vm = Create();
        Assert.False(vm.CanSave);

        vm.Target = Notepad;
        Assert.True(vm.CanSave); // the name was guessed
    }

    [Fact]
    public void ToEntry_copies_every_option()
    {
        var original = new HotkeyEntry { Name = "Me", Target = Notepad, LaunchCount = 7 };
        var vm = Create(original);
        vm.Capture(CtrlAlt, VkB);
        vm.RunHidden = true;
        vm.IfRunning = RunningBehavior.BringToFront;
        vm.Placement = WindowPlacement.RightHalf;
        vm.Monitor = 2;

        var entry = vm.ToEntry();

        Assert.Equal(original.Id, entry.Id);
        Assert.Equal(new Hotkey(CtrlAlt, VkB), entry.Hotkey);
        Assert.True(entry.RunHidden);
        Assert.Equal(RunningBehavior.BringToFront, entry.IfRunning);
        Assert.Equal(WindowPlacement.RightHalf, entry.Placement);
        Assert.Equal(2, entry.Monitor);
        Assert.Equal(7, entry.LaunchCount); // statistics are kept
    }

    [Fact]
    public void Bring_to_front_is_offered_for_programs_only()
    {
        var vm = Create();
        vm.Target = Notepad;
        Assert.True(vm.CanBringToFront);

        vm.Target = "https://github.com";
        Assert.False(vm.CanBringToFront);
    }

    private static EditorViewModel With(Action<EditorViewModel> action)
    {
        var vm = Create();
        action(vm);
        return vm;
    }
}
