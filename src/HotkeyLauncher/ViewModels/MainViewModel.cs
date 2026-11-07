using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotkeyLauncher.Core;
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
    private readonly DispatcherTimer _undoTimer = new() { Interval = TimeSpan.FromSeconds(8) };
    private (List<HotkeyEntry> Hotkeys, List<string> Groups)? _undoSnapshot;

    public MainViewModel(AppController app)
    {
        _app = app;
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(HotkeyItemViewModel.Group)));
        ItemsView.Filter = item => string.IsNullOrWhiteSpace(SearchText) || ((HotkeyItemViewModel)item).Matches(SearchText.Trim());
        ApplySort();

        _undoTimer.Tick += (_, _) => DismissUndo();
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

    public bool HasFailures => _app.RegistrationFailures.Keys.Any(id => id != HotkeyService.QuickLauncherId);

    // ---- Banner ----

    public string ShortcutCountText => F("Header_Shortcuts", _app.Settings.Hotkeys.Count);

    public string GroupCountText => F("Header_Groups", _app.Settings.Groups.Count);

    public string StatusText => _app.HotkeysActive ? T("Header_Active") : T("Header_Paused");

    // ---- Sort ----

    [ObservableProperty]
    public partial IReadOnlyList<Option<ListSortMode>> SortOptions { get; private set; } = [];

    public ListSortMode SelectedSort
    {
        get => _app.Settings.SortMode;
        set
        {
            if (value == _app.Settings.SortMode) return;
            _app.Settings.SortMode = value;
            _app.SaveSettings();
            ApplySort();
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsReorderEnabled));
        }
    }

    /// <summary>Drag &amp; drop reordering is only possible in the custom order.</summary>
    public bool IsReorderEnabled => _app.Settings.SortMode == ListSortMode.Custom && string.IsNullOrWhiteSpace(SearchText);

    // ---- Undo after delete ----

    [ObservableProperty]
    public partial string? UndoMessage { get; private set; }

    // ---- Settings page ----

    [ObservableProperty]
    public partial IReadOnlyList<Option<string>> LanguageOptions { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Option<AppTheme>> ThemeOptions { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Option<KeyboardShape>> KeyboardShapeOptions { get; private set; } = [];

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

    public KeyboardShape SelectedKeyboardShape
    {
        get => _app.Settings.KeyboardShape;
        set
        {
            if (value == _app.Settings.KeyboardShape) return;
            _app.Settings.KeyboardShape = value;
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
                Log.Info($"Start with Windows: {value}");
            }
            catch (Exception ex)
            {
                Log.Error("Cannot change the start with Windows setting", ex);
                _ = Dialogs.InfoAsync(T("Settings_StartWithWindows"), ex.Message);
            }

            OnPropertyChanged();
        }
    }

    public bool QuickLauncherEnabled
    {
        get => _app.Settings.QuickLauncherEnabled;
        set
        {
            if (value == _app.Settings.QuickLauncherEnabled) return;
            _app.Settings.QuickLauncherEnabled = value;
            _app.SaveSettings();
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<string> QuickLauncherHotkeyParts => KeyNames.Parts(_app.Settings.QuickLauncherHotkey);

    /// <summary>"Ctrl + Alt + Space", shown on the toolbar button.</summary>
    public string QuickLauncherHotkeyText => KeyNames.Format(_app.Settings.QuickLauncherHotkey);

    public bool QuickLauncherFailed => _app.QuickLauncherFailed;

    public bool CheckForUpdates
    {
        get => _app.Settings.CheckForUpdates;
        set
        {
            if (value == _app.Settings.CheckForUpdates) return;
            _app.Settings.CheckForUpdates = value;
            _app.SaveSettings();
            OnPropertyChanged();
        }
    }

    public string SettingsFilePath => _app.SettingsFilePath;

    // ---- About page ----

    public static string Version => AppController.CurrentVersion.ToString(3);

    public UpdateInfo? AvailableUpdate => _app.AvailableUpdate;

    public string? UpdateText => _app.AvailableUpdate is { } update ? F("Update_AvailableText", update.Tag) : null;

    [ObservableProperty]
    public partial bool IsCheckingUpdates { get; private set; }

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
        copy.ResetStatistics();
        _app.Settings.Hotkeys.Insert(_app.Settings.Hotkeys.IndexOf(item.Entry) + 1, copy);
        _app.SaveSettings();
    }

    /// <summary>Deletes immediately; an "Undo" bar stays a few seconds.</summary>
    public void Delete(HotkeyItemViewModel item)
    {
        _undoSnapshot = ([.. _app.Settings.Hotkeys], [.. _app.Settings.Groups]);
        _app.Settings.Hotkeys.Remove(item.Entry);
        _app.SaveSettings();
        Log.Info($"Shortcut deleted: \"{item.Name}\"");

        UndoMessage = F("Undo_Deleted", item.Name);
        _undoTimer.Stop();
        _undoTimer.Start();
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

    /// <summary>Drag &amp; drop: puts <paramref name="item"/> before or after <paramref name="target"/>, in the target's group.</summary>
    public void MoveTo(HotkeyItemViewModel item, HotkeyItemViewModel target, bool before)
    {
        if (item == target) return;
        var list = _app.Settings.Hotkeys;
        list.Remove(item.Entry);
        var index = list.IndexOf(target.Entry);
        if (index < 0) return;
        item.Entry.Group = target.Entry.Group;
        list.Insert(before ? index : index + 1, item.Entry);
        _app.SaveSettings();
    }

    /// <summary>Drag &amp; drop on a group header: moves the entry at the end of that group.</summary>
    public void MoveToGroup(HotkeyItemViewModel item, string group)
    {
        var list = _app.Settings.Hotkeys;
        list.Remove(item.Entry);
        item.Entry.Group = group;
        var last = list.FindLastIndex(h => h.Group == group);
        list.Insert(last < 0 ? list.Count : last + 1, item.Entry);
        _app.SaveSettings();
    }

    public void SetEnabled(HotkeyItemViewModel item, bool enabled)
    {
        if (enabled && _app.Settings.Hotkeys.FirstOrDefault(h => h != item.Entry && h.IsEnabled && !h.Hotkey.IsEmpty && h.Hotkey == item.Entry.Hotkey) is { } owner)
        {
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
    private void Undo()
    {
        if (_undoSnapshot is not { } snapshot) return;
        _app.Settings.Hotkeys.Clear();
        _app.Settings.Hotkeys.AddRange(snapshot.Hotkeys);
        _app.Settings.Groups.Clear();
        _app.Settings.Groups.AddRange(snapshot.Groups);
        _app.SaveSettings();
        DismissUndo();
    }

    [RelayCommand]
    private void DismissUndo()
    {
        _undoTimer.Stop();
        _undoSnapshot = null;
        UndoMessage = null;
    }

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
    private void ChangeQuickLauncherHotkey()
    {
        if (!_app.ChangeQuickLauncherHotkey()) return;
        OnPropertyChanged(nameof(QuickLauncherEnabled));
        OnPropertyChanged(nameof(QuickLauncherHotkeyParts));
        OnPropertyChanged(nameof(QuickLauncherHotkeyText));
    }

    [RelayCommand]
    private void OpenQuickLauncher() => _app.ShowQuickLauncher();

    [RelayCommand]
    private void OpenSettingsFolder()
    {
        if (File.Exists(SettingsFilePath))
        {
            Process.Start("explorer.exe", $"/select,\"{SettingsFilePath}\"")?.Dispose();
        }
        else
        {
            AppController.OpenUrl(_app.SettingsDirectory);
        }
    }

    [RelayCommand]
    private void OpenLog()
    {
        if (Log.FilePath is { } path && File.Exists(path)) AppController.OpenUrl(path);
        else OpenSettingsFolder();
    }

    [RelayCommand]
    private static void OpenWebsite() => AppController.OpenUrl(WebsiteUrl);

    [RelayCommand]
    private void DownloadUpdate() => AppController.OpenUrl(_app.AvailableUpdate?.Url ?? UpdateChecker.ReleasesPage);

    [RelayCommand]
    private async Task CheckUpdatesNowAsync()
    {
        IsCheckingUpdates = true;
        try
        {
            var (update, error) = await _app.CheckForUpdatesAsync();
            if (error != null)
            {
                await Dialogs.InfoAsync(T("Update_Title"), F("Update_Error", error.Message));
            }
            else if (update == null)
            {
                await Dialogs.InfoAsync(T("Update_Title"), F("Update_UpToDate", Version));
            }
            else
            {
                var choice = await Dialogs.ShowAsync(T("Update_Title"), F("Update_AvailableText", update.Tag), T("Update_Download"), null, T("Common_Cancel"));
                if (choice == UiMessageBoxResult.Primary) AppController.OpenUrl(update.Url);
            }
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    [RelayCommand]
    private void Exit() => _app.Exit();

    /// <summary>Saves the shortcuts in a file, to copy them to another computer or share them.</summary>
    [RelayCommand]
    private async Task ExportAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = T("Transfer_ExportTitle"),
            Filter = $"{T("Transfer_FileType")} (*{SettingsTransfer.FileExtension})|*{SettingsTransfer.FileExtension}|JSON (*.json)|*.json",
            FileName = $"HotkeyLauncher-{DateTime.Now:yyyy-MM-dd}{SettingsTransfer.FileExtension}",
        };
        if (dialog.ShowDialog(Application.Current.MainWindow) != true) return;

        try
        {
            SettingsTransfer.Export(_app.Settings, dialog.FileName);
            Log.Info($"Shortcuts exported to {dialog.FileName}");
            await Dialogs.InfoAsync(T("Transfer_ExportTitle"), F("Transfer_ExportDone", _app.Settings.Hotkeys.Count, dialog.FileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error("Export failed", ex);
            await Dialogs.InfoAsync(T("Transfer_ExportTitle"), ex.Message);
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = T("Transfer_ImportTitle"),
            Filter = $"{T("Transfer_FileType")} (*{SettingsTransfer.FileExtension};*.json)|*{SettingsTransfer.FileExtension};*.json|* (*.*)|*.*",
        };
        if (dialog.ShowDialog(Application.Current.MainWindow) != true) return;

        ImportResult import;
        try
        {
            import = SettingsTransfer.Import(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Log.Error($"Import of {dialog.FileName} failed", ex);
            await Dialogs.InfoAsync(T("Transfer_ImportTitle"), F("Import_ReadError", ex.Message));
            return;
        }

        await MergeImportAsync(T("Transfer_ImportTitle"), import);
    }

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

        ImportResult import;
        try
        {
            import = RapidAppImporter.Import(dialog.FileName);
        }
        catch (Exception ex)
        {
            Log.Error($"RapidApp import of {dialog.FileName} failed", ex);
            await Dialogs.InfoAsync(T("Import_Title"), F("Import_ReadError", ex.Message));
            return;
        }

        await MergeImportAsync(T("Import_Title"), import);
    }

    private async Task MergeImportAsync(string title, ImportResult import)
    {
        if (import.Entries.Count == 0)
        {
            await Dialogs.InfoAsync(title, T("Import_Nothing"));
            return;
        }

        var replace = false;
        if (_app.Settings.Hotkeys.Count > 0)
        {
            var choice = await Dialogs.ShowAsync(
                title,
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
        Log.Info($"{merge.Added} shortcut(s) imported (replace: {replace})");

        var message = F("Import_Done", merge.Added);
        var withoutModifier = import.Entries.Count(e => !e.Hotkey.IsEmpty && !e.Hotkey.IsComplete);
        if (merge.DisabledBecauseOfConflict > 0) message += Environment.NewLine + F("Import_DoneConflicts", merge.DisabledBecauseOfConflict);
        if (withoutModifier > 0) message += Environment.NewLine + F("Import_DoneIncomplete", withoutModifier);
        await Dialogs.InfoAsync(title, message);
    }

    partial void OnSearchTextChanged(string value)
    {
        ItemsView.Refresh();
        OnPropertyChanged(nameof(IsReorderEnabled));
    }

    private void MoveGroup(string group, int direction)
    {
        var groups = _app.Settings.Groups;
        var index = groups.IndexOf(group);
        var other = index + direction;
        if (index < 0 || other < 0 || other >= groups.Count) return;
        (groups[index], groups[other]) = (groups[other], groups[index]);
        _app.SaveSettings();
    }

    /// <summary>Groups always keep their order; inside a group the entries follow the chosen sort.</summary>
    private void ApplySort()
    {
        using (ItemsView.DeferRefresh())
        {
            ItemsView.SortDescriptions.Clear();
            ItemsView.SortDescriptions.Add(new SortDescription(nameof(HotkeyItemViewModel.GroupOrder), ListSortDirection.Ascending));
            switch (_app.Settings.SortMode)
            {
                case ListSortMode.Name:
                    ItemsView.SortDescriptions.Add(new SortDescription(nameof(HotkeyItemViewModel.Name), ListSortDirection.Ascending));
                    break;
                case ListSortMode.MostUsed:
                    ItemsView.SortDescriptions.Add(new SortDescription(nameof(HotkeyItemViewModel.LaunchCount), ListSortDirection.Descending));
                    break;
                case ListSortMode.RecentlyUsed:
                    ItemsView.SortDescriptions.Add(new SortDescription(nameof(HotkeyItemViewModel.LastLaunched), ListSortDirection.Descending));
                    break;
            }

            ItemsView.SortDescriptions.Add(new SortDescription(nameof(HotkeyItemViewModel.Order), ListSortDirection.Ascending));
        }
    }

    private void Reload()
    {
        var existing = Items.ToDictionary(i => i.Entry.Id);
        Items.Clear();
        var order = 0;
        foreach (var entry in _app.Settings.Hotkeys)
        {
            var item = existing.TryGetValue(entry.Id, out var vm) ? vm : new HotkeyItemViewModel(this, entry);
            item.Update(entry, _app.RegistrationFailures.TryGetValue(entry.Id, out var error) ? error : null, _app.Settings.GroupIndex(entry.Group), order++);
            Items.Add(item);
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HotkeysActive));
        OnPropertyChanged(nameof(HasFailures));
        OnPropertyChanged(nameof(ShortcutCountText));
        OnPropertyChanged(nameof(GroupCountText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(QuickLauncherFailed));
        OnPropertyChanged(nameof(QuickLauncherHotkeyParts));
        OnPropertyChanged(nameof(QuickLauncherHotkeyText));
        OnPropertyChanged(nameof(AvailableUpdate));
        OnPropertyChanged(nameof(UpdateText));
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
        KeyboardShapeOptions =
        [
            new(KeyboardShape.Auto, T("Settings_KeyboardAuto")),
            new(KeyboardShape.Iso, T("Settings_KeyboardIso")),
            new(KeyboardShape.Ansi, T("Settings_KeyboardAnsi")),
        ];
        SortOptions =
        [
            new(ListSortMode.Custom, T("Sort_Custom")),
            new(ListSortMode.Name, T("Sort_Name")),
            new(ListSortMode.MostUsed, T("Sort_MostUsed")),
            new(ListSortMode.RecentlyUsed, T("Sort_Recent")),
        ];
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(SelectedKeyboardShape));
        OnPropertyChanged(nameof(SelectedSort));
    }
}
