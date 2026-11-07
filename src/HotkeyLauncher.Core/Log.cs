namespace HotkeyLauncher.Core;

/// <summary>
/// Minimal file log (HotkeyLauncher.log, next to the settings file) to diagnose problems on a user's computer.
/// The file is rotated at 1 MB (the previous one is kept as .old).
/// </summary>
public static class Log
{
    public const string FileName = "HotkeyLauncher.log";
    private const long MaxSize = 1024 * 1024;
    private static readonly Lock Sync = new();

    public static string? FilePath { get; private set; }

    public static void Initialize(string directory)
    {
        FilePath = Path.Combine(directory, FileName);
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warning(string message) => Write("WARN", message);

    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception == null ? message : $"{message}{Environment.NewLine}{exception}");

    private static void Write(string level, string message)
    {
        if (FilePath == null) return;
        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > MaxSize) File.Move(FilePath, FilePath + ".old", overwrite: true);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Logging must never break the application
            }
        }
    }
}
