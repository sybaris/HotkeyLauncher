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
    private bool _exiting;
    private bool _watchingSystemTheme;

    public AppController(SettingsStore store, AppSettings settings)
    {
        _store = store;
        Settings = settings;
        Startup = new StartupRegistration("HotkeyLauncher", Environment.ProcessPath ?? string.Empty, TrayArgument);

        _hotkeys.HotkeyPressed += (_, id) => Launch(id);
        _tray.OpenRequested += (_, _) => ShowMainWindow();
        _tray.ToggleRequested += (_, _) => SetHotkeysActive(!HotkeysActive);
        _tray.ExitRequested += (_, _) => Exit();
        _tray.LaunchRequested += (_, id) => Launch(id);
        Localizer.Instance.LanguageChanged += (_, _) => RefreshTray();
    }

    /// <summary>Raised after any change of the shortcuts or of their registration state.</summary>
    public event EventHandler? Changed;

    public AppSettings Settings { get; }

    public string SettingsFilePath => _store.FilePath;

    public StartupRegistration Startup { get; }

    /// <summary>Global switch (tray menu / main window): when false no hotkey is registered.</summary>
    public bool HotkeysActive { get; private set; } = true;

    /// <summary>Entries whose hotkey could not be registered (already used elsewhere), with the Win32 error.</summary>
    public IReadOnlyDictionary<Guid, int> RegistrationFailures { get; private set; } = new Dictionary<Guid, int>();

    public static bool IsDarkTheme => ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;

    public void Start(bool showWindow)
    {
        Startup.RepairIfMoved();
        ApplyTheme(Settings.Theme);
        ApplyHotkeys();
        _tray.Show();

        if (showWindow)
        {
            ShowMainWindow();
        }
        else if (RegistrationFailures.Count > 0)
        {
            _tray.ShowNotification("HotkeyLauncher", F("Tray_RegistrationFailures", RegistrationFailures.Count), isError: true);
        }
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

    /// <summary>Saves the settings file then re-registers the hotkeys.</summary>
    public void SaveSettings()
    {
        try
        {
            _store.Save(Settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _tray.ShowNotification(T("Error_SaveTitle"), ex.Message, isError: true);
        }

        ApplyHotkeys();
    }

    public void SetHotkeysActive(bool active)
    {
        if (HotkeysActive == active) return;
        HotkeysActive = active;
        ApplyHotkeys();
    }

    /// <summary>Opens the editor. Returns the edited copy, or null if cancelled. The caller saves it.</summary>
    public HotkeyEntry? EditEntry(HotkeyEntry? existing, string? initialTarget = null)
    {
        var entry = existing?.Clone() ?? new HotkeyEntry { Target = initialTarget ?? string.Empty };
        var others = Settings.Hotkeys.Where(h => h.Id != entry.Id).ToList();

        // While editing: our hotkeys must not fire when the user types a combination, and the
        // availability probe must only see the other programs.
        _hotkeys.UnregisterAll();
        try
        {
            var viewModel = new EditorViewModel(entry, others, Settings.Groups, _hotkeys.IsAvailable, isNew: existing == null);
            var window = new HotkeyEditorWindow(viewModel);
            if (_mainWindow is { IsVisible: true })
            {
                window.Owner = _mainWindow;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            return window.ShowDialog() == true ? viewModel.ToEntry() : null;
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
            Launcher.Launch(entry);
        }
        catch (Exception ex)
        {
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

    public void Exit()
    {
        _exiting = true;
        _mainWindow?.Close();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _hotkeys.Dispose();
        _tray.Dispose();
    }

    private void ApplyHotkeys()
    {
        if (HotkeysActive)
        {
            RegistrationFailures = _hotkeys.RegisterAll(Settings.Hotkeys);
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
