using System.ComponentModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotkeyLauncher.Core;
using HotkeyLauncher.Core.Launching;
using HotkeyLauncher.Core.Models;
using HotkeyLauncher.Services;
using static HotkeyLauncher.Localization.Localizer;

namespace HotkeyLauncher.ViewModels;

public enum DropPosition
{
    None,
    Before,
    After,
}

/// <summary>One line of the shortcut list.</summary>
public sealed partial class HotkeyItemViewModel : ObservableObject
{
    private readonly MainViewModel _owner;

    public HotkeyItemViewModel(MainViewModel owner, HotkeyEntry entry)
    {
        _owner = owner;
        Entry = entry;
    }

    public HotkeyEntry Entry { get; private set; }

    public string Name => Entry.Name;

    /// <summary>Grouping key of the list (an empty group is displayed as "No group").</summary>
    public string Group => Entry.Group;

    public string Target => string.IsNullOrWhiteSpace(Entry.Arguments) ? Entry.Target : $"{Entry.Target}  {Entry.Arguments}";

    public ImageSource? Icon => IconService.GetImage(Entry.Target);

    public IReadOnlyList<string> HotkeyParts => KeyNames.Parts(Entry.Hotkey);

    public bool HasHotkey => !Entry.Hotkey.IsEmpty;

    public bool RunsAsAdministrator => Entry.RunAsAdministrator;

    public bool IsEnabled
    {
        get => Entry.IsEnabled;
        set
        {
            if (Entry.IsEnabled == value) return;
            _owner.SetEnabled(this, value);
            OnPropertyChanged();
        }
    }

    /// <summary>Problem to show next to the line (hotkey already used, target missing...), or null.</summary>
    [ObservableProperty]
    public partial string? Warning { get; private set; }

    /// <summary>Insertion marker shown while another shortcut is dragged over this one.</summary>
    [ObservableProperty]
    public partial DropPosition DropIndicator { get; set; }

    // ---- Sorting (see MainViewModel.ApplySort) ----

    public int GroupOrder { get; private set; }

    /// <summary>Position in the custom order.</summary>
    public int Order { get; private set; }

    public int LaunchCount => Entry.LaunchCount;

    public DateTime LastLaunched => Entry.LastLaunchedUtc ?? DateTime.MinValue;

    /// <summary>"12 launches · 2 h ago".</summary>
    public string UsageText => Entry.LaunchCount == 0
        ? T("Usage_Never")
        : F("Usage_Text", Entry.LaunchCount, RelativeTime(Entry.LastLaunchedUtc));

    public void Update(HotkeyEntry entry, int? registrationError, int groupOrder, int order)
    {
        Entry = entry;
        GroupOrder = groupOrder;
        Order = order;
        DropIndicator = DropPosition.None;
        Warning = registrationError switch
        {
            1409 => T("Item_HotkeyTakenElsewhere"), // ERROR_HOTKEY_ALREADY_REGISTERED
            int code => F("Item_HotkeyRegistrationError", new Win32Exception(code).Message),
            null when TargetResolver.Classify(entry.Target) == TargetKind.NotFound => T("Item_TargetNotFound"),
            _ => null,
        };
        OnPropertyChanged(string.Empty);
    }

    public bool Matches(string search) =>
        Entry.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
        || Entry.Group.Contains(search, StringComparison.CurrentCultureIgnoreCase)
        || Entry.Target.Contains(search, StringComparison.CurrentCultureIgnoreCase)
        || KeyNames.Format(Entry.Hotkey).Contains(search, StringComparison.CurrentCultureIgnoreCase);

    private static string RelativeTime(DateTime? utc)
    {
        if (utc == null) return string.Empty;
        var elapsed = DateTime.UtcNow - utc.Value;
        if (elapsed < TimeSpan.FromMinutes(1)) return T("Time_JustNow");
        if (elapsed < TimeSpan.FromHours(1)) return F("Time_Minutes", (int)elapsed.TotalMinutes);
        if (elapsed < TimeSpan.FromDays(1)) return F("Time_Hours", (int)elapsed.TotalHours);
        if (elapsed < TimeSpan.FromDays(30)) return F("Time_Days", (int)elapsed.TotalDays);
        return utc.Value.ToLocalTime().ToString("d", Localization.Localizer.Instance.Culture);
    }

    [RelayCommand]
    private void Edit() => _owner.Edit(this);

    [RelayCommand]
    private void Launch() => _owner.Launch(this);

    [RelayCommand]
    private void Duplicate() => _owner.Duplicate(this);

    [RelayCommand]
    private void Delete() => _owner.Delete(this);

    [RelayCommand]
    private void MoveUp() => _owner.Move(this, -1);

    [RelayCommand]
    private void MoveDown() => _owner.Move(this, +1);
}
