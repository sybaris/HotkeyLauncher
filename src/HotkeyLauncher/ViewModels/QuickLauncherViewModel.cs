using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotkeyLauncher.Core;
using HotkeyLauncher.Core.Models;
using HotkeyLauncher.Services;
using static HotkeyLauncher.Localization.Localizer;

namespace HotkeyLauncher.ViewModels;

/// <summary>One result of the quick launcher.</summary>
public sealed partial class QuickResultViewModel(HotkeyEntry entry) : ObservableObject
{
    public HotkeyEntry Entry { get; } = entry;

    public string Name => Entry.Name;

    public string Subtitle => Entry.Group.Length > 0 ? $"{Entry.Group}  ·  {Entry.Target}" : Entry.Target;

    public ImageSource? Icon => IconService.GetImage(Entry.Target);

    public IReadOnlyList<string> HotkeyParts => Entry.IsEnabled ? KeyNames.Parts(Entry.Hotkey) : [];

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>Search box opened by a global hotkey: type a few letters, press Enter.</summary>
public sealed partial class QuickLauncherViewModel(AppController app) : ObservableObject
{
    private const int MaxResults = 8;

    public event EventHandler? CloseRequested;

    public ObservableCollection<QuickResultViewModel> Results { get; } = [];

    [ObservableProperty]
    public partial string Query { get; set; } = string.Empty;

    public bool HasResults => Results.Count > 0;

    public string Header => string.IsNullOrWhiteSpace(Query) ? T("QuickLauncher_Recent") : F("QuickLauncher_ResultCount", Results.Count);

    /// <summary>"Ctrl + Alt + Space", reminded in the footer.</summary>
    public string HotkeyText => KeyNames.Format(app.Settings.QuickLauncherHotkey);

    private int _selectedIndex;

    public void Reset()
    {
        if (Query.Length > 0) Query = string.Empty;
        else Refresh();
    }

    public void Refresh()
    {
        Results.Clear();
        foreach (var entry in QuickSearch.Search(app.Settings.Hotkeys, Query, MaxResults)) Results.Add(new QuickResultViewModel(entry));
        Select(0);
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(HotkeyText));
    }

    public void MoveSelection(int delta)
    {
        if (Results.Count == 0) return;
        Select((_selectedIndex + delta + Results.Count) % Results.Count);
    }

    public void LaunchSelected()
    {
        if (_selectedIndex >= 0 && _selectedIndex < Results.Count) Launch(Results[_selectedIndex]);
    }

    [RelayCommand]
    private void Launch(QuickResultViewModel result)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        app.Launch(result.Entry);
    }

    /// <summary>Footer button: closes the launcher and asks for a new key combination.</summary>
    [RelayCommand]
    private void ChangeHotkey()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        app.ChangeQuickLauncherHotkey();
    }

    partial void OnQueryChanged(string value) => Refresh();

    private void Select(int index)
    {
        _selectedIndex = Results.Count == 0 ? -1 : index;
        for (var i = 0; i < Results.Count; i++) Results[i].IsSelected = i == _selectedIndex;
    }
}
