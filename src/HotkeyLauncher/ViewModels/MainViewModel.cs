using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotkeyLauncher.Core.Import;
using HotkeyLauncher.Core.Models;
using HotkeyLauncher.Localization;
using HotkeyLauncher.Services;
using HotkeyLauncher.Views;
using static HotkeyLauncher.Localization.Localizer;
using UiMessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace HotkeyLauncher.ViewModels;

public sealed record Option<T>(T Value, string Label);

public sealed partial class MainViewModel : ObservableObject
{
    public const string WebsiteUrl = "https://github.com/sybaris/HotkeyLauncher";

    private readonly AppController _app;

    public MainViewModel(AppController app)
    {
        _app = app;
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(HotkeyItemViewModel.Group)));
        ItemsView.Filter = item => string.IsNullOrWhiteSpace(SearchText) || ((HotkeyItemViewModel)item).Matches(SearchText.Trim());

        _app.Changed += (_, _) => Reload();
        Localizer.Instance.LanguageChanged += (_, _) =>
        {
            BuildOptions();
            Reload();
        };

        BuildOptions();
        Reload();
    }

    public ObservableCollection<HotkeyItemViewModel> Items { get; } = [];

    public ICollectionView ItemsView { get; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public bool IsEmpty => Items.Count == 0;

    public bool HotkeysActive
    {
        get => _app.HotkeysActive;
        set => _app.SetHotkeysActive(value);
    }

    public bool HasFailures => _app.RegistrationFailures.Count > 0;

    // ---- Banner ----

    public string ShortcutCountText => F("Header_Shortcuts", _app.Settings.Hotkeys.Count);

    public string GroupCountText => F("Header_Groups", _app.Settings.Groups.Count);

    public string StatusText => _app.HotkeysActive ? T("Header_Active") : T("Header_Paused");

    // ---- Settings page ----

    [ObservableProperty]
    public partial IReadOnlyList<Option<string>> LanguageOptions { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Option<AppTheme>> ThemeOptions { get; private set; } = [];

    public string SelectedLanguage
    {
        get => _app.Settings.Language;
        set
        {
            if (value == null || value == _app.Settings.Language) return;
            _app.Settings.Language = value;
            Localizer.Instance.SetLanguage(value);
            _app.SaveSettings();
            OnPropertyChanged();
        }
    }

    public AppTheme SelectedTheme
    {
        get => _app.Settings.Theme;
        set
        {
            if (value == _app.Settings.Theme) return;
            _app.Settings.Theme = value;
            _app.ApplyTheme(value);
            _app.SaveSettings();
            OnPropertyChanged();
        }
    }

    public bool StartWithWindows
    {
        get => _app.Startup.IsEnabled;
        set
        {
            try
            {
                if (value) _app.Startup.Enable();
                else _app.Startup.Disable();
            }
            catch (Exception ex)
            {
                _ = Dialogs.InfoAsync(T("Settings_StartWithWindows"), ex.Message);
            }

            OnPropertyChanged();
        }
    }

    public string SettingsFilePath => _app.SettingsFilePath;

    public static string Version
    {
        get
        {
            var version = typeof(MainViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";
            var plus = version.IndexOf('+');
            return plus > 0 ? version[..plus] : version;
        }
    }

    // ---- Shortcut operations (called by the list items) ----

    public void Edit(HotkeyItemViewModel item)
    {
        var edited = _app.EditEntry(item.Entry);
        if (edited == null) return;
        var index = _app.Settings.Hotkeys.FindIndex(h => h.Id == edited.Id);
        if (index >= 0) _app.Settings.Hotkeys[index] = edited;
        IconService.ClearCache();
        _app.SaveSettings();
    }

    public void Launch(HotkeyItemViewModel item) => _app.Launch(item.Entry);

    public void Duplicate(HotkeyItemViewModel item)
    {
        var copy = item.Entry.Clone();
        copy.Id = Guid.NewGuid();
        copy.Name = F("Item_CopyName", copy.Name);
        copy.Hotkey = Hotkey.None; // a hotkey cannot be used twice
        _app.Settings.Hotkeys.Insert(_app.Settings.Hotkeys.IndexOf(item.Entry) + 1, copy);
        _app.SaveSettings();
    }

    public async Task DeleteAsync(HotkeyItemViewModel item)
    {
        var result = await Dialogs.ShowAsync(T("Delete_Title"), F("Delete_Message", item.Name), T("Delete_Confirm"), null, T("Common_Cancel"));
        if (result != UiMessageBoxResult.Primary) return;
        _app.Settings.Hotkeys.Remove(item.Entry);
        _app.SaveSettings();
    }

    /// <summary>Moves the entry up or down inside its group.</summary>
    public void Move(HotkeyItemViewModel item, int direction)
    {
        var list = _app.Settings.Hotkeys;
        var index = list.IndexOf(item.Entry);
        var other = index + direction;
        if (index < 0 || other < 0 || other >= list.Count || list[other].Group != item.Entry.Group) return;
        (list[index], list[other]) = (list[other], list[index]);
        _app.SaveSettings();
    }

    public void SetEnabled(HotkeyItemViewModel item, bool enabled)
    {
        if (enabled && _app.Settings.Hotkeys.Any(h => h != item.Entry && h.IsEnabled && !h.Hotkey.IsEmpty && h.Hotkey == item.Entry.Hotkey))
        {
            var owner = _app.Settings.Hotkeys.First(h => h != item.Entry && h.IsEnabled && h.Hotkey == item.Entry.Hotkey);
            _ = Dialogs.InfoAsync(T("Item_EnableConflictTitle"), F("Item_EnableConflict", owner.Name));
            return;
        }

        item.Entry.IsEnabled = enabled;
        _app.SaveSettings();
    }

    /// <summary>A file, folder or link dropped on the window: open the editor pre-filled.</summary>
    public void AddFromTarget(string target)
    {
        var entry = _app.EditEntry(null, target);
        if (entry == null) return;
        _app.Settings.Hotkeys.Add(entry);
        _app.SaveSettings();
    }

    [RelayCommand]
    private void Add() => AddFromTarget(string.Empty);

    [RelayCommand]
    private async Task RenameGroupAsync(string group)
    {
        if (string.IsNullOrEmpty(group)) return;
        var newName = TextPromptWindow.Ask(Application.Current.MainWindow, T("Group_RenameTitle"), T("Group_RenameLabel"), group);
        if (string.IsNullOrWhiteSpace(newName) || newName.Trim() == group) return;
        newName = newName.Trim();

        if (_app.Settings.Groups.Contains(newName, StringComparer.CurrentCultureIgnoreCase))
        {
            var result = await Dialogs.ShowAsync(T("Group_RenameTitle"), F("Group_MergeMessage", group, newName), T("Group_Merge"), null, T("Common_Cancel"));
            if (result != UiMessageBoxResult.Primary) return;
        }

        var index = _app.Settings.Groups.IndexOf(group);
        if (index >= 0) _app.Settings.Groups[index] = newName;
        foreach (var entry in _app.Settings.Hotkeys.Where(h => h.Group == group)) entry.Group = newName;
        _app.SaveSettings();
    }

    [RelayCommand]
    private void MoveGroupUp(string group) => MoveGroup(group, -1);

    [RelayCommand]
    private void MoveGroupDown(string group) => MoveGroup(group, +1);

    [RelayCommand]
    private void OpenSettingsFolder()
    {
        if (File.Exists(SettingsFilePath))
        {
            Process.Start("explorer.exe", $"/select,\"{SettingsFilePath}\"")?.Dispose();
        }
        else
        {
            Process.Start(new ProcessStartInfo(Path.GetDirectoryName(SettingsFilePath)!) { UseShellExecute = true })?.Dispose();
        }
    }

    [RelayCommand]
    private static void OpenWebsite() => Process.Start(new ProcessStartInfo(WebsiteUrl) { UseShellExecute = true })?.Dispose();

    [RelayCommand]
    private void Exit() => _app.Exit();

    /// <summary>Migration from RapidApp (the Delphi ancestor): reads RapidApp.ini.</summary>
    [RelayCommand]
    private async Task ImportRapidAppAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = T("Import_DialogTitle"),
            Filter = $"RapidApp ({RapidAppImporter.DefaultFileName})|{RapidAppImporter.DefaultFileName}|INI (*.ini)|*.ini|* (*.*)|*.*",
            FileName = RapidAppImporter.DefaultFileName,
        };
        if (dialog.ShowDialog(Application.Current.MainWindow) != true) return;

        RapidAppImportResult import;
        try
        {
            import = RapidAppImporter.Import(dialog.FileName);
        }
        catch (Exception ex)
        {
            await Dialogs.InfoAsync(T("Import_Title"), F("Import_ReadError", ex.Message));
            return;
        }

        if (import.Entries.Count == 0)
        {
            await Dialogs.InfoAsync(T("Import_Title"), T("Import_Nothing"));
            return;
        }

        var replace = false;
        if (_app.Settings.Hotkeys.Count > 0)
        {
            var choice = await Dialogs.ShowAsync(
                T("Import_Title"),
                F("Import_ExistingQuestion", import.Entries.Count, _app.Settings.Hotkeys.Count),
                T("Import_Add"),
                T("Import_Replace"),
                T("Common_Cancel"));
            if (choice == UiMessageBoxResult.None) return;
            replace = choice == UiMessageBoxResult.Secondary;
        }

        var merge = SettingsMerger.Merge(_app.Settings, import, replace);
        IconService.ClearCache();
        _app.SaveSettings();

        var message = F("Import_Done", merge.Added);
        var withoutModifier = import.Entries.Count(e => !e.Hotkey.IsComplete);
        if (merge.DisabledBecauseOfConflict > 0) message += Environment.NewLine + F("Import_DoneConflicts", merge.DisabledBecauseOfConflict);
        if (withoutModifier > 0) message += Environment.NewLine + F("Import_DoneIncomplete", withoutModifier);
        await Dialogs.InfoAsync(T("Import_Title"), message);
    }

    partial void OnSearchTextChanged(string value) => ItemsView.Refresh();

    private void MoveGroup(string group, int direction)
    {
        var groups = _app.Settings.Groups;
        var index = groups.IndexOf(group);
        var other = index + direction;
        if (index < 0 || other < 0 || other >= groups.Count) return;
        (groups[index], groups[other]) = (groups[other], groups[index]);
        _app.SaveSettings();
    }

    private void Reload()
    {
        var existing = Items.ToDictionary(i => i.Entry.Id);
        Items.Clear();
        foreach (var entry in _app.Settings.Hotkeys)
        {
            var item = existing.TryGetValue(entry.Id, out var vm) ? vm : new HotkeyItemViewModel(this, entry);
            item.Update(entry, _app.RegistrationFailures.TryGetValue(entry.Id, out var error) ? error : null);
            Items.Add(item);
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HotkeysActive));
        OnPropertyChanged(nameof(HasFailures));
        OnPropertyChanged(nameof(ShortcutCountText));
        OnPropertyChanged(nameof(GroupCountText));
        OnPropertyChanged(nameof(StatusText));
    }

    private void BuildOptions()
    {
        LanguageOptions = [new(string.Empty, T("Settings_LanguageSystem")), .. Localizer.Languages.Select(l => new Option<string>(l.Code, l.Name))];
        ThemeOptions =
        [
            new(AppTheme.System, T("Settings_ThemeSystem")),
            new(AppTheme.Light, T("Settings_ThemeLight")),
            new(AppTheme.Dark, T("Settings_ThemeDark")),
        ];
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(SelectedTheme));
    }
}
