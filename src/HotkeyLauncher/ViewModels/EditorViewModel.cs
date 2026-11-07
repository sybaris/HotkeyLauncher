using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotkeyLauncher.Core;
using HotkeyLauncher.Core.Launching;
using HotkeyLauncher.Core.Models;
using HotkeyLauncher.Services;
using static HotkeyLauncher.Localization.Localizer;

namespace HotkeyLauncher.ViewModels;

public enum HotkeyStatus
{
    None,
    NeedsModifier,
    Available,
    UsedByShortcut,
    ReservedByWindows,
    Taken,
}

public sealed record HotkeySuggestion(Hotkey Hotkey, string Text);

/// <summary>Edition of one shortcut, with the hotkey picker (capture + on-screen keyboard).</summary>
public sealed partial class EditorViewModel : ObservableObject
{
    private readonly HotkeyEntry _original;
    private readonly IReadOnlyList<HotkeyEntry> _others;
    private readonly Func<Hotkey, bool> _isAvailable;
    private readonly Dictionary<Hotkey, bool> _availabilityCache = [];
    private string _autoName = string.Empty;

    public EditorViewModel(HotkeyEntry entry, IReadOnlyList<HotkeyEntry> others, IReadOnlyList<string> groups, Func<Hotkey, bool> isAvailable, bool isNew)
    {
        _original = entry;
        _others = others;
        _isAvailable = isAvailable;
        IsNew = isNew;
        Groups = groups;

        MainKeyboard = [.. KeyboardLayout.MainBlock.Select(row => row.Select(k => new KeyViewModel(k)).ToList())];
        NavigationKeyboard = [.. KeyboardLayout.NavigationBlock.Select(row => row.Select(k => new KeyViewModel(k)).ToList())];

        WindowStates =
        [
            new(LaunchWindowState.Normal, T("Editor_WindowNormal")),
            new(LaunchWindowState.Maximized, T("Editor_WindowMaximized")),
            new(LaunchWindowState.Minimized, T("Editor_WindowMinimized")),
        ];

        Name = entry.Name;
        Group = entry.Group;
        Target = entry.Target;
        Arguments = entry.Arguments;
        WorkingDirectory = entry.WorkingDirectory;
        WindowState = entry.WindowState;
        RunAsAdministrator = entry.RunAsAdministrator;
        Modifiers = entry.Modifiers;
        VirtualKey = entry.VirtualKey;
        ShowAdvanced = !string.IsNullOrEmpty(entry.Arguments) || !string.IsNullOrEmpty(entry.WorkingDirectory)
            || entry.WindowState != LaunchWindowState.Normal || entry.RunAsAdministrator;

        if (isNew && Name.Length == 0) ApplyAutoName();
        Refresh();
    }

    /// <summary>Asks the view to close (true = saved).</summary>
    public event EventHandler<bool>? CloseRequested;

    public bool IsNew { get; }

    public string Title => IsNew ? T("Editor_TitleNew") : T("Editor_TitleEdit");

    public IReadOnlyList<string> Groups { get; }

    public IReadOnlyList<Option<LaunchWindowState>> WindowStates { get; }

    public IReadOnlyList<IReadOnlyList<KeyViewModel>> MainKeyboard { get; }

    public IReadOnlyList<IReadOnlyList<KeyViewModel>> NavigationKeyboard { get; }

    private IEnumerable<KeyViewModel> AllKeys => MainKeyboard.Concat(NavigationKeyboard).SelectMany(row => row);

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string Group { get; set; }

    [ObservableProperty]
    public partial string Target { get; set; }

    [ObservableProperty]
    public partial string Arguments { get; set; }

    [ObservableProperty]
    public partial string WorkingDirectory { get; set; }

    [ObservableProperty]
    public partial LaunchWindowState WindowState { get; set; }

    [ObservableProperty]
    public partial bool RunAsAdministrator { get; set; }

    [ObservableProperty]
    public partial bool ShowAdvanced { get; set; }

    [ObservableProperty]
    public partial HotkeyModifiers Modifiers { get; set; }

    [ObservableProperty]
    public partial uint VirtualKey { get; set; }

    public Hotkey Hotkey => new(Modifiers, VirtualKey);

    public IReadOnlyList<string> HotkeyParts => KeyNames.Parts(Hotkey);

    public bool HasHotkey => !Hotkey.IsEmpty;

    public bool IsCtrl { get => Has(HotkeyModifiers.Control); set => SetModifier(HotkeyModifiers.Control, value); }
    public bool IsAlt { get => Has(HotkeyModifiers.Alt); set => SetModifier(HotkeyModifiers.Alt, value); }
    public bool IsShift { get => Has(HotkeyModifiers.Shift); set => SetModifier(HotkeyModifiers.Shift, value); }
    public bool IsWin { get => Has(HotkeyModifiers.Win); set => SetModifier(HotkeyModifiers.Win, value); }

    public ImageSource? TargetIcon => IconService.GetImage(Target);

    public TargetKind TargetKind => TargetResolver.Classify(Target);

    public string TargetKindText => TargetKind switch
    {
        TargetKind.Application => T("Target_Application"),
        TargetKind.File => T("Target_File"),
        TargetKind.Folder => T("Target_Folder"),
        TargetKind.Url => T("Target_Url"),
        TargetKind.Command => T("Target_Command"),
        TargetKind.NotFound => T("Target_NotFound"),
        _ => T("Target_Hint"),
    };

    public bool IsTargetWarning => TargetKind == TargetKind.NotFound;

    [ObservableProperty]
    public partial HotkeyStatus HotkeyStatus { get; private set; }

    [ObservableProperty]
    public partial string HotkeyStatusText { get; private set; } = string.Empty;

    public Wpf.Ui.Controls.SymbolRegular StatusSymbol => HotkeyStatus switch
    {
        HotkeyStatus.Available => Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24,
        HotkeyStatus.UsedByShortcut => Wpf.Ui.Controls.SymbolRegular.ErrorCircle24,
        HotkeyStatus.None => Wpf.Ui.Controls.SymbolRegular.Info24,
        _ => Wpf.Ui.Controls.SymbolRegular.Warning24,
    };

    partial void OnHotkeyStatusChanged(HotkeyStatus value) => OnPropertyChanged(nameof(StatusSymbol));

    [ObservableProperty]
    public partial IReadOnlyList<HotkeySuggestion> Suggestions { get; private set; } = [];

    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    [ObservableProperty]
    public partial string? WarningText { get; private set; }

    [ObservableProperty]
    public partial bool CanSave { get; private set; }

    /// <summary>Combination typed in the capture box. Without physical modifier, the toggled modifiers are kept.</summary>
    public void Capture(HotkeyModifiers pressedModifiers, uint virtualKey)
    {
        if (pressedModifiers != HotkeyModifiers.None) Modifiers = pressedModifiers;
        VirtualKey = virtualKey;
    }

    /// <summary>The Windows keyboard layout changed (Win+Space): relabel the on-screen keyboard.</summary>
    public void RefreshKeyboardLayout()
    {
        foreach (var key in AllKeys) key.RefreshLayout();
        Refresh();
    }

    public HotkeyEntry ToEntry()
    {
        var entry = _original.Clone();
        // Properties may still be null while the constructor initializes them
        entry.Name = Name?.Trim() ?? string.Empty;
        entry.Group = Group?.Trim() ?? string.Empty;
        entry.Target = Target?.Trim() ?? string.Empty;
        entry.Arguments = Arguments?.Trim() ?? string.Empty;
        entry.WorkingDirectory = WorkingDirectory?.Trim() ?? string.Empty;
        entry.WindowState = WindowState;
        entry.RunAsAdministrator = RunAsAdministrator;
        entry.Hotkey = Hotkey;
        if (IsNew) entry.IsEnabled = true;
        return entry;
    }

    [RelayCommand]
    private void KeyClicked(KeyViewModel key)
    {
        if (key.Kind == KeyKind.Modifier)
        {
            SetModifier(key.Modifier, !Has(key.Modifier));
        }
        else if (key.Kind == KeyKind.Normal)
        {
            VirtualKey = key.VirtualKey;
        }
    }

    [RelayCommand]
    private void ApplySuggestion(HotkeySuggestion suggestion)
    {
        Modifiers = suggestion.Hotkey.Modifiers;
        VirtualKey = suggestion.Hotkey.VirtualKey;
    }

    [RelayCommand]
    private void ClearHotkey()
    {
        Modifiers = HotkeyModifiers.None;
        VirtualKey = 0;
    }

    [RelayCommand]
    private void BrowseFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = T("Editor_BrowseFileTitle"),
            Filter = $"{T("Editor_FilterPrograms")}|*.exe;*.bat;*.cmd;*.com;*.lnk;*.msc;*.ps1|{T("Editor_FilterAll")}|*.*",
            DereferenceLinks = false,
        };
        if (TargetResolver.ResolvePath(Target) is { } current && File.Exists(current)) dialog.InitialDirectory = Path.GetDirectoryName(current);
        if (dialog.ShowDialog() == true) Target = dialog.FileName;
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = T("Editor_BrowseFolderTitle") };
        if (dialog.ShowDialog() == true) Target = dialog.FolderName;
    }

    [RelayCommand]
    private void BrowseWorkingDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = T("Editor_WorkingDirectory") };
        if (dialog.ShowDialog() == true) WorkingDirectory = dialog.FolderName;
    }

    [RelayCommand]
    private void Test()
    {
        try
        {
            Launcher.Launch(ToEntry());
        }
        catch (Exception ex)
        {
            _ = Dialogs.InfoAsync(T("Editor_Test"), ex.Message);
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (CanSave) CloseRequested?.Invoke(this, true);
    }

    partial void OnTargetChanged(string value)
    {
        ApplyAutoName();
        OnPropertyChanged(nameof(TargetIcon));
        OnPropertyChanged(nameof(TargetKind));
        OnPropertyChanged(nameof(TargetKindText));
        OnPropertyChanged(nameof(IsTargetWarning));
        Validate();
    }

    /// <summary>Name shown in the preview banner.</summary>
    public string PreviewName => string.IsNullOrWhiteSpace(Name) ? Title : Name;

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(PreviewName));
        Validate();
    }

    partial void OnWorkingDirectoryChanged(string value) => Validate();

    partial void OnModifiersChanged(HotkeyModifiers value) => Refresh();

    partial void OnVirtualKeyChanged(uint value) => Refresh();

    /// <summary>Proposes a name from the target as long as the user did not type one.</summary>
    private void ApplyAutoName()
    {
        if (Name is { Length: > 0 } && Name != _autoName) return;
        _autoName = GuessName(Target);
        Name = _autoName;
    }

    private static string GuessName(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) return string.Empty;
        if (TargetResolver.IsUrl(target))
        {
            return Uri.TryCreate(target.Trim(), UriKind.Absolute, out var uri) && uri.Host.Length > 0
                ? uri.Host.Replace("www.", string.Empty, StringComparison.OrdinalIgnoreCase)
                : target.Trim();
        }

        var path = TargetResolver.ResolvePath(target);
        if (path is not null && File.Exists(path) && Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var description = FileVersionInfo.GetVersionInfo(path).FileDescription;
                if (!string.IsNullOrWhiteSpace(description)) return description.Trim();
            }
            catch (IOException)
            {
                // No version information: use the file name
            }
        }

        var expanded = TargetResolver.Expand(target).TrimEnd('\\', '/');
        var name = Directory.Exists(expanded) ? Path.GetFileName(expanded) : Path.GetFileNameWithoutExtension(expanded);
        return string.IsNullOrEmpty(name) ? expanded : name;
    }

    private bool Has(HotkeyModifiers modifier) => Modifiers.HasFlag(modifier);

    private void SetModifier(HotkeyModifiers modifier, bool on) => Modifiers = on ? Modifiers | modifier : Modifiers & ~modifier;

    private bool IsAvailable(Hotkey hotkey)
    {
        if (!_availabilityCache.TryGetValue(hotkey, out var available))
        {
            available = !ReservedHotkeys.IsReserved(hotkey) && _isAvailable(hotkey);
            _availabilityCache[hotkey] = available;
        }

        return available;
    }

    /// <summary>Recomputes everything that depends on the hotkey: keyboard colors, status, suggestions.</summary>
    private void Refresh()
    {
        OnPropertyChanged(nameof(Hotkey));
        OnPropertyChanged(nameof(HotkeyParts));
        OnPropertyChanged(nameof(HasHotkey));
        OnPropertyChanged(nameof(IsCtrl));
        OnPropertyChanged(nameof(IsAlt));
        OnPropertyChanged(nameof(IsShift));
        OnPropertyChanged(nameof(IsWin));

        foreach (var key in AllKeys)
        {
            key.RefreshLabel();
            UpdateKey(key);
        }

        UpdateStatus();
        UpdateSuggestions();
        Validate();
    }

    private void UpdateKey(KeyViewModel key)
    {
        key.ToolTip = null;
        key.OwnerIcon = null;

        if (key.Kind == KeyKind.Modifier)
        {
            key.State = Has(key.Modifier) ? KeyState.ModifierOn : KeyState.ModifierOff;
            return;
        }

        if (key.Kind != KeyKind.Normal) return;

        var hotkey = new Hotkey(Modifiers, key.VirtualKey);
        if (key.VirtualKey == VirtualKey)
        {
            key.State = KeyState.Selected;
        }
        else if (!hotkey.IsComplete)
        {
            key.State = KeyState.Neutral;
        }
        else if (EntryValidator.FindOwner(hotkey, _others) is { } owner)
        {
            key.State = KeyState.UsedByShortcut;
            key.ToolTip = F("Editor_KeyUsedBy", owner.Name);
            key.OwnerIcon = IconService.GetImage(owner.Target);
        }
        else if (!IsAvailable(hotkey))
        {
            key.State = KeyState.Taken;
            key.ToolTip = ReservedHotkeys.IsReserved(hotkey) ? T("Editor_KeyReserved") : T("Editor_KeyTaken");
        }
        else
        {
            key.State = KeyState.Free;
            key.ToolTip = KeyNames.Format(hotkey);
        }
    }

    private void UpdateStatus()
    {
        var hotkey = Hotkey;
        (HotkeyStatus, HotkeyStatusText) = hotkey switch
        {
            { IsEmpty: true } => (HotkeyStatus.None, T("Editor_StatusNone")),
            { IsComplete: false } => (HotkeyStatus.NeedsModifier, T("Editor_StatusNeedsModifier")),
            _ when EntryValidator.FindOwner(hotkey, _others) is { } owner => (HotkeyStatus.UsedByShortcut, F("Editor_StatusUsedBy", owner.Name)),
            _ when ReservedHotkeys.IsReserved(hotkey) => (HotkeyStatus.ReservedByWindows, T("Editor_StatusReserved")),
            _ when !IsAvailable(hotkey) => (HotkeyStatus.Taken, T("Editor_StatusTaken")),
            _ => (HotkeyStatus.Available, T("Editor_StatusAvailable")),
        };
    }

    private void UpdateSuggestions()
    {
        if (HotkeyStatus == HotkeyStatus.Available)
        {
            Suggestions = [];
            return;
        }

        var modifiers = Modifiers == HotkeyModifiers.None ? HotkeyModifiers.Control | HotkeyModifiers.Alt : Modifiers;
        Suggestions = [.. KeyboardLayout.SuggestionCandidates()
            .Select(vk => new Hotkey(modifiers, vk))
            .Where(h => EntryValidator.FindOwner(h, _others) == null && IsAvailable(h))
            .Take(5)
            .Select(h => new HotkeySuggestion(h, KeyNames.Format(h)))];
    }

    private void Validate()
    {
        var issues = EntryValidator.Validate(ToEntry(), _others);
        ErrorText = issues.Where(EntryValidator.IsError).Select(IssueText).FirstOrDefault();
        var warnings = issues.Where(i => !EntryValidator.IsError(i)).Select(IssueText).ToList();
        if (HotkeyStatus is HotkeyStatus.Taken or HotkeyStatus.ReservedByWindows) warnings.Add(T("Editor_WarningTaken"));
        WarningText = warnings.Count > 0 ? string.Join(Environment.NewLine, warnings) : null;
        CanSave = ErrorText == null;
    }

    private static string IssueText(ValidationIssue issue) => issue switch
    {
        ValidationIssue.NameMissing => T("Validation_NameMissing"),
        ValidationIssue.TargetMissing => T("Validation_TargetMissing"),
        ValidationIssue.HotkeyNeedsModifier => T("Editor_StatusNeedsModifier"),
        ValidationIssue.HotkeyAlreadyUsed => T("Validation_HotkeyAlreadyUsed"),
        ValidationIssue.TargetNotFound => T("Target_NotFound"),
        ValidationIssue.WorkingDirectoryNotFound => T("Validation_WorkingDirectoryNotFound"),
        _ => issue.ToString(),
    };
}
