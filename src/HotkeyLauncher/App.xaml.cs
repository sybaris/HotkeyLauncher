using System.Resources;
using System.Windows;
using System.Windows.Threading;
using HotkeyLauncher.Core;
using HotkeyLauncher.Core.Models;
using HotkeyLauncher.Core.Settings;
using HotkeyLauncher.Localization;
using HotkeyLauncher.Services;
using static HotkeyLauncher.Localization.Localizer;

[assembly: NeutralResourcesLanguage("en")]
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

namespace HotkeyLauncher;

/// <summary>
/// Command line:
///   (none)          open the main window
///   /setup, --setup open the main window (RapidApp compatible), also on an already running instance
///   --tray          start hidden in the notification area (used by "start with Windows")
///   /?, --help      display the help
/// </summary>
public partial class App : Application
{
    private SingleInstance? _singleInstance;
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        var store = SettingsStore.CreateDefault();
        var isFirstRun = !store.Exists;
        Log.Initialize(store.Directory);
        var settings = LoadSettings(store);
        Localizer.Instance.SetLanguage(settings.Language);

        var args = e.Args.Select(a => a.Trim().ToLowerInvariant()).ToArray();
        if (args.Any(a => a is "/?" or "/h" or "/help" or "-h" or "--help" or "-?"))
        {
            MessageBox.Show(T("CommandLine_Help"), "HotkeyLauncher", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _singleInstance = new SingleInstance();
        if (!_singleInstance.IsFirstInstance)
        {
            _singleInstance.SignalFirstInstance();
            Shutdown();
            return;
        }

        Log.Info($"HotkeyLauncher {AppController.CurrentVersion} started ({Environment.ProcessPath}), settings: {store.FilePath}, {settings.Hotkeys.Count} shortcut(s)");
        var startInTray = args.Contains(AppController.TrayArgument) && !isFirstRun;
        _controller = new AppController(store, settings);
        _singleInstance.Listen(() => Dispatcher.BeginInvoke(_controller.ShowMainWindow));
        _controller.Start(showWindow: !startInTray, isFirstRun);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_controller != null) Log.Info("HotkeyLauncher stopped");
        _controller?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private static AppSettings LoadSettings(SettingsStore store)
    {
        try
        {
            return store.Load();
        }
        catch (SettingsLoadException ex)
        {
            var backup = store.BackupCorruptedFile();
            Log.Error($"Invalid settings file, renamed to {backup}", ex);
            Localizer.Instance.SetLanguage(string.Empty);
            MessageBox.Show(F("Error_SettingsCorrupted", ex.Message, backup), "HotkeyLauncher", MessageBoxButton.OK, MessageBoxImage.Warning);
            return new AppSettings();
        }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unexpected error", e.Exception);
        MessageBox.Show(F("Error_Unexpected", e.Exception.Message), "HotkeyLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
