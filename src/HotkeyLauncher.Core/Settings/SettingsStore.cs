using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using HotkeyLauncher.Core.Models;

namespace HotkeyLauncher.Core.Settings;

public sealed class SettingsLoadException(string message, Exception inner) : Exception(message, inner);

/// <summary>
/// Reads and writes the settings file. The application is portable: the file lives next to the executable,
/// unless that folder is read-only (e.g. Program Files), in which case %APPDATA%\HotkeyLauncher is used.
/// </summary>
public sealed class SettingsStore(string filePath)
{
    public const string FileName = "HotkeyLauncher.settings.json";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Number of automatic backups kept.</summary>
    public const int BackupCount = 3;

    private bool _backupDone;

    public string FilePath { get; } = filePath;

    public string Directory => Path.GetDirectoryName(FilePath)!;

    /// <summary>Automatic backups: the settings file as it was at the start of the last sessions.</summary>
    public string BackupDirectory => Path.Combine(Directory, "backups");

    public bool Exists => File.Exists(FilePath);

    /// <summary>Picks the portable location when possible.</summary>
    public static SettingsStore CreateDefault()
    {
        var exeDirectory = AppContext.BaseDirectory;
        var portablePath = Path.Combine(exeDirectory, FileName);
        if (File.Exists(portablePath) || (!IsManagedByPackageManager(exeDirectory) && IsDirectoryWritable(exeDirectory)))
        {
            return new SettingsStore(portablePath);
        }

        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HotkeyLauncher");
        return new SettingsStore(Path.Combine(appData, FileName));
    }

    public AppSettings Load()
    {
        if (!Exists) return new AppSettings();

        try
        {
            // An empty file (e.g. created by a package manager) means "no settings yet"
            var json = File.ReadAllText(FilePath);
            var settings = string.IsNullOrWhiteSpace(json) ? new AppSettings() : JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new SettingsLoadException($"The settings file \"{FilePath}\" is invalid: {ex.Message}", ex);
        }
    }

    /// <summary>Writes atomically (temporary file then rename) so a crash never leaves a truncated file.</summary>
    public void Save(AppSettings settings)
    {
        settings.Normalize();
        System.IO.Directory.CreateDirectory(Directory);
        BackupOncePerSession();
        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(tempPath, FilePath, overwrite: true);
    }

    /// <summary>Renames an unreadable file so that the application can start with fresh settings.</summary>
    public string BackupCorruptedFile()
    {
        var backup = $"{FilePath}.corrupted-{DateTime.Now:yyyyMMdd-HHmmss}";
        File.Move(FilePath, backup);
        return backup;
    }

    /// <summary>
    /// Before the first save of each session, copies the current file to backups/ and keeps only the
    /// <see cref="BackupCount"/> most recent copies.
    /// </summary>
    private void BackupOncePerSession()
    {
        if (_backupDone || !Exists) return;
        _backupDone = true;
        try
        {
            System.IO.Directory.CreateDirectory(BackupDirectory);
            File.Copy(FilePath, Path.Combine(BackupDirectory, $"settings-{DateTime.Now:yyyyMMdd-HHmmss}.json"), overwrite: true);
            foreach (var old in System.IO.Directory.GetFiles(BackupDirectory, "settings-*.json").OrderDescending().Skip(BackupCount))
            {
                File.Delete(old);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning($"Settings backup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// True for a folder installed by winget or Chocolatey: such a folder is replaced or removed by updates and
    /// uninstallation, so the settings go to %APPDATA% instead. (Scoop keeps them next to the executable through
    /// its "persist" feature.)
    /// </summary>
    public static bool IsManagedByPackageManager(string directory)
    {
        var path = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        if (path.Contains(@"\WinGet\Packages\", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.Contains(@"\chocolatey\lib\", StringComparison.OrdinalIgnoreCase)) return true;
        var chocolatey = Environment.GetEnvironmentVariable("ChocolateyInstall");
        return !string.IsNullOrEmpty(chocolatey)
               && path.StartsWith(Path.Combine(chocolatey, "lib") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDirectoryWritable(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
