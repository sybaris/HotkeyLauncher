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

    public string FilePath { get; } = filePath;

    public bool Exists => File.Exists(FilePath);

    /// <summary>Picks the portable location when possible.</summary>
    public static SettingsStore CreateDefault()
    {
        var exeDirectory = AppContext.BaseDirectory;
        var portablePath = Path.Combine(exeDirectory, FileName);
        if (File.Exists(portablePath) || IsDirectoryWritable(exeDirectory))
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
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings();
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
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
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
