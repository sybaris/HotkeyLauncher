using System.Diagnostics;
using System.Reflection;
using System.Windows;
using HotkeyLauncher.Core;
using HotkeyLauncher.Core.Launching;
using HotkeyLauncher.Core.Models;
using HotkeyLauncher.Core.Settings;
using HotkeyLauncher.Localization;
using HotkeyLauncher.Services;
using HotkeyLauncher.ViewModels;
using HotkeyLauncher.Views;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using static HotkeyLauncher.Localization.Localizer;

namespace HotkeyLauncher;

/// <summary>Application core: owns the settings, the registered hotkeys, the tray icon and the windows.</summary>
public sealed class AppController : IDisposable
{
    /// <summary>Argument used by the "start with Windows" registration: start hidden in the notification area.</summary>
    public const string TrayArgument = "--tray";

    private readonly SettingsStore _store;
    private readonly HotkeyService _hotkeys = new();
    private readonly TrayService _tray = new();
    private MainWindow? _mainWindow;
    private QuickLauncherWindow? _quickLauncher;
    private bool _exiting;
    private bool _watchingSystemTheme;

    public AppController(SettingsStore store, AppSettings settings)
    {
        _store = store;
        Settings = settings;
        Startup = new StartupRegistration("HotkeyLauncher", Environment.ProcessPath ?? string.Empty, TrayArgument);

        _hotkeys.HotkeyPressed += (_, id) => Launch(id);
        _hotkeys.QuickLauncherPressed += (_, _) => ShowQuickLauncher();
        _tray.OpenRequested += (_, _) => ShowMainWindow();
        _tray.ToggleRequested += (_, _) => SetHotkeysActive(!HotkeysActive);
        _tray.ExitRequested += (_, _) => Exit();
        _tray.LaunchRequested += (_, id) => Launch(id);
        Localizer.Instance.LanguageChanged += (_, _) => RefreshTray();
    }

    /// <summary>Raised after any change of the shortcuts, of their registration state or of their statistics.</summary>
    public event EventHandler? Changed;

    public AppSettings Settings { get; }

    public string SettingsFilePath => _store.FilePath;

    public string SettingsDirectory => _store.Directory;

    public StartupRegistration Startup { get; }

    /// <summary>Global switch (tray menu / main window): when false no hotkey is registered.</summary>
    public bool HotkeysActive { get; private set; } = true;

    /// <summary>Entries whose hotkey could not be registered (already used elsewhere), with the Win32 error.</summary>
    public IReadOnlyDictionary<Guid, int> RegistrationFailures { get; private set; } = new Dictionary<Guid, int>();

    public bool QuickLauncherFailed => RegistrationFailures.ContainsKey(HotkeyService.QuickLauncherId);

    /// <summary>Newer version found on GitHub, if any.</summary>
    public UpdateInfo? AvailableUpdate { get; private set; }

    public static Version CurrentVersion { get; } = ReadCurrentVersion();

    public static bool IsDarkTheme => ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;

    public void Start(bool showWindow, bool isFirstRun = false)
    {
        if (Startup.RepairIfMoved()) Log.Info($"Start with Windows: path updated to {Environment.ProcessPath}");
        ApplyTheme(Settings.Theme);
        var quickLauncherChosen = ChooseDefaultQuickLauncherHotkey(isFirstRun);
        ApplyHotkeys();
        _tray.Show();
        if (quickLauncherChosen)
        {
            _tray.ShowNotification(T("QuickLauncher_Name"), F("QuickLauncher_DefaultChosen", KeyNames.Format(Settings.QuickLauncherHotkey)), isError: false);
        }

        if (showWindow)
        {
            ShowMainWindow();
        }
        else if (RegistrationFailures.Count > 0)
        {
            _tray.ShowNotification("HotkeyLauncher", F("Tray_RegistrationFailures", RegistrationFailures.Count), isError: true);
        }

        ScheduleAutomaticUpdateCheck();
    }

    /// <summary>
    /// On the first run (or for settings still using the former default Ctrl+Alt+Space), takes the first
    /// default quick launcher hotkey that no other program uses. Returns true if the hotkey was chosen.
    /// </summary>
    private bool ChooseDefaultQuickLauncherHotkey(bool isFirstRun)
    {
        var formerDefault = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x20);
        var migrate = Settings.SchemaVersion < 2 && Settings.QuickLauncherHotkey == formerDefault;
        if (Settings.SchemaVersion < AppSettings.CurrentSchemaVersion) Settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
        if (!isFirstRun && !migrate) return false;

        var used = Settings.Hotkeys.Where(h => h.IsEnabled).Select(h => h.Hotkey).ToHashSet();
        var choice = AppSettings.QuickLauncherCandidates.FirstOrDefault(h => !used.Contains(h) && _hotkeys.IsAvailable(h));
        if (choice.IsEmpty) choice = AppSettings.QuickLauncherCandidates[0];
        Settings.QuickLauncherHotkey = choice;
        Log.Info($"Quick launcher hotkey: {KeyNames.Format(choice)}");
        if (!isFirstRun) WriteSettings();
        return true;
    }

    public void ShowMainWindow()
    {
        if (_mainWindow == null)
        {
            _mainWindow = new MainWindow(new MainViewModel(this));
            _mainWindow.Closing += (_, e) =>
            {
                if (_exiting) return;
                // Closing the window keeps the application running in the notification area
                e.Cancel = true;
                _mainWindow.Hide();
                if (!Settings.TrayTipShown)
                {
                    Settings.TrayTipShown = true;
                    SaveSettings();
                    _tray.ShowNotification("HotkeyLauncher", T("Tray_StillRunning"));
                }
            };
            _mainWindow.Loaded += (_, _) => WatchSystemTheme();
            Application.Current.MainWindow = _mainWindow;
        }

        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized) _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public void ShowQuickLauncher()
    {
        _quickLauncher ??= new QuickLauncherWindow(new QuickLauncherViewModel(this));
        _quickLauncher.ShowCentered();
    }

    /// <summary>Saves the settings file then re-registers the hotkeys.</summary>
    public void SaveSettings()
    {
        WriteSettings();
        ApplyHotkeys();
    }

    public void SetHotkeysActive(bool active)
    {
        if (HotkeysActive == active) return;
        HotkeysActive = active;
        Log.Info(active ? "Shortcuts enabled" : "Shortcuts disabled");
        ApplyHotkeys();
    }

    /// <summary>Opens the editor. Returns the edited copy, or null if cancelled. The caller saves it.</summary>
    public HotkeyEntry? EditEntry(HotkeyEntry? existing, string? initialTarget = null)
    {
        var entry = existing?.Clone() ?? new HotkeyEntry { Target = initialTarget ?? string.Empty };
        var others = Settings.Hotkeys.Where(h => h.Id != entry.Id).ToList();
        if (Settings.QuickLauncherEnabled)
        {
            // The quick launcher hotkey is shown as used, like the other shortcuts
            others.Add(new HotkeyEntry { Name = T("QuickLauncher_Name"), Hotkey = Settings.QuickLauncherHotkey });
        }

        // While editing: our hotkeys must not fire when the user types a combination, and the
        // availability probe must only see the other programs.
        _hotkeys.UnregisterAll();
        try
        {
            var viewModel = new EditorViewModel(entry, others, Settings.Groups, _hotkeys.IsAvailable, existing == null, Settings.KeyboardShape, Settings.ShowNumpad);
            var window = new HotkeyEditorWindow(viewModel);
            SetOwner(window);
            var saved = window.ShowDialog() == true;

            if (viewModel.ShowNumpad != Settings.ShowNumpad)
            {
                Settings.ShowNumpad = viewModel.ShowNumpad;
                WriteSettings();
            }

            return saved ? viewModel.ToEntry() : null;
        }
        finally
        {
            ApplyHotkeys();
        }
    }

    /// <summary>Asks for a new quick launcher hotkey. Returns null if cancelled.</summary>
    /// <summary>Asks for a new quick launcher hotkey, then saves it. Returns false if cancelled.</summary>
    public bool ChangeQuickLauncherHotkey()
    {
        if (EditQuickLauncherHotkey() is not { } hotkey) return false;
        Settings.QuickLauncherHotkey = hotkey;
        Settings.QuickLauncherEnabled = true;
        SaveSettings();
        return true;
    }

    public Hotkey? EditQuickLauncherHotkey()
    {
        _hotkeys.UnregisterAll();
        try
        {
            var window = new HotkeyPromptWindow(Settings.QuickLauncherHotkey, Settings.Hotkeys, _hotkeys.IsAvailable);
            SetOwner(window);
            return window.ShowDialog() == true ? window.Hotkey : null;
        }
        finally
        {
            ApplyHotkeys();
        }
    }

    public void Launch(Guid id)
    {
        var entry = Settings.Hotkeys.FirstOrDefault(h => h.Id == id);
        if (entry != null) Launch(entry);
    }

    public void Launch(HotkeyEntry entry)
    {
        try
        {
            var result = Launcher.Launch(entry);
            if (result == LaunchResult.Cancelled) return;

            entry.LaunchCount++;
            entry.LastLaunchedUtc = DateTime.UtcNow;
            WriteSettings();
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error($"Cannot launch \"{entry.Name}\" ({entry.Target})", ex);
            _tray.ShowNotification(F("Launch_ErrorTitle", entry.Name), ex.Message, isError: true);
        }
    }

    public void ApplyTheme(AppTheme theme)
    {
        switch (theme)
        {
            case AppTheme.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light, WindowBackdropType.Mica);
                break;
            case AppTheme.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica);
                break;
            default:
                ApplicationThemeManager.ApplySystemTheme();
                break;
        }

        WatchSystemTheme();
        RefreshTray();
    }

    /// <summary>Asks GitHub for a newer version. Never throws: returns the error instead.</summary>
    public async Task<(UpdateInfo? Update, Exception? Error)> CheckForUpdatesAsync()
    {
        try
        {
            var update = await UpdateChecker.CheckAsync(CurrentVersion);
            Settings.LastUpdateCheckUtc = DateTime.UtcNow;
            WriteSettings();
            AvailableUpdate = update;
            if (update != null) Log.Info($"New version available: {update.Tag}");
            Changed?.Invoke(this, EventArgs.Empty);
            return (update, null);
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            Log.Warning($"Update check failed: {ex.Message}");
            return (null, ex);
        }
    }

    public static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();

    public void Exit()
    {
        _exiting = true;
        _quickLauncher?.Close();
        _mainWindow?.Close();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _hotkeys.Dispose();
        _tray.Dispose();
    }

    /// <summary>Writes the file without re-registering the hotkeys (statistics, preferences).</summary>
    private void WriteSettings()
    {
        try
        {
            _store.Save(Settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error("Cannot save the settings", ex);
            _tray.ShowNotification(T("Error_SaveTitle"), ex.Message, isError: true);
        }
    }

    private void ApplyHotkeys()
    {
        if (HotkeysActive)
        {
            RegistrationFailures = _hotkeys.RegisterAll(Settings.Hotkeys, Settings.QuickLauncherEnabled ? Settings.QuickLauncherHotkey : null);
            foreach (var (id, error) in RegistrationFailures)
            {
                var name = id == HotkeyService.QuickLauncherId ? "quick launcher" : Settings.Hotkeys.FirstOrDefault(h => h.Id == id)?.Name;
                Log.Warning($"Hotkey of \"{name}\" not registered (Win32 error {error})");
            }
        }
        else
        {
            _hotkeys.UnregisterAll();
            RegistrationFailures = new Dictionary<Guid, int>();
        }

        RefreshTray();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshTray() => _tray.Update(Settings, HotkeysActive, IsDarkTheme);

    /// <summary>Checks once a day, a little after the start (never blocks the startup).</summary>
    private void ScheduleAutomaticUpdateCheck()
    {
        if (!Settings.CheckForUpdates) return;
        if (Settings.LastUpdateCheckUtc is { } last && DateTime.UtcNow - last < TimeSpan.FromDays(1)) return;

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            var (update, _) = await CheckForUpdatesAsync();
            if (update != null)
            {
                _tray.ShowNotification(T("Update_AvailableTitle"), F("Update_AvailableText", update.Tag), onClick: () => OpenUrl(update.Url));
            }
        };
        timer.Start();
    }

    private void SetOwner(Window window)
    {
        if (_mainWindow is not { IsVisible: true }) return;
        window.Owner = _mainWindow;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
    }

    private static Version ReadCurrentVersion()
    {
        var text = typeof(AppController).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var end = text.IndexOfAny(['+', '-']);
        return Version.TryParse(end > 0 ? text[..end] : text, out var version) ? version : new Version(0, 0, 0);
    }

    /// <summary>Follows the Windows light/dark setting live, only when the theme is "Windows setting".</summary>
    private void WatchSystemTheme()
    {
        if (_mainWindow is not { IsLoaded: true }) return;
        var watch = Settings.Theme == AppTheme.System;
        if (watch == _watchingSystemTheme) return;

        if (watch) SystemThemeWatcher.Watch(_mainWindow);
        else SystemThemeWatcher.UnWatch(_mainWindow);
        _watchingSystemTheme = watch;
    }
}
