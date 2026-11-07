using Microsoft.Win32;

namespace HotkeyLauncher.Core.Launching;

public enum TargetKind
{
    /// <summary>Nothing entered.</summary>
    Empty,
    /// <summary>Executable program (.exe, shortcut...).</summary>
    Application,
    /// <summary>Script (.ps1, .bat, .cmd, .vbs...).</summary>
    Script,
    /// <summary>Document opened with its associated program.</summary>
    File,
    Folder,
    /// <summary>Web address or other URI (https:, mailto:, ms-settings:...).</summary>
    Url,
    /// <summary>Command found in the PATH or App Paths (e.g. "notepad", "wt").</summary>
    Command,
    /// <summary>The path does not exist (yet). It may still work later (removable drive...).</summary>
    NotFound,
}

public static class TargetResolver
{
    private static readonly string[] ExecutableExtensions = [".exe", ".com", ".lnk", ".msc", ".cpl", ".appref-ms"];
    private static readonly string[] ScriptExtensions = [".ps1", ".bat", ".cmd", ".vbs", ".js", ".wsf"];

    /// <summary>Expands %VARIABLES% and removes surrounding quotes.</summary>
    public static string Expand(string value) => Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));

    public static bool IsUrl(string target)
    {
        var value = target.Trim();
        // "C:\..." is parsed as a file URI and "c:" as a scheme: require a scheme of 2+ chars that is not "file"
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme.Length > 1
            && !uri.IsFile
            && !uri.IsUnc;
    }

    public static TargetKind Classify(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) return TargetKind.Empty;
        if (IsUrl(target)) return TargetKind.Url;

        var path = Expand(target);
        if (Directory.Exists(path)) return TargetKind.Folder;
        if (File.Exists(path)) return IsScript(path) ? TargetKind.Script : IsExecutable(path) ? TargetKind.Application : TargetKind.File;
        if (!Path.IsPathRooted(path) && FindCommand(path) != null) return TargetKind.Command;
        return TargetKind.NotFound;
    }

    /// <summary>Full path of the file or folder to launch, when it can be determined (used for icons and names).</summary>
    public static string? ResolvePath(string target)
    {
        if (string.IsNullOrWhiteSpace(target) || IsUrl(target)) return null;
        var path = Expand(target);
        if (File.Exists(path) || Directory.Exists(path)) return Path.GetFullPath(path);
        return Path.IsPathRooted(path) ? null : FindCommand(path);
    }

    public static bool IsExecutable(string path) =>
        ExecutableExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static bool IsScript(string path) =>
        ScriptExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static bool IsPowerShellScript(string path) =>
        Path.GetExtension(path).Equals(".ps1", StringComparison.OrdinalIgnoreCase);

    /// <summary>Looks for a command the same way the Run dialog does: App Paths registry, then PATH with PATHEXT.</summary>
    public static string? FindCommand(string command)
    {
        if (command.Length == 0 || command.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || command.Contains('\\') || command.Contains('/')) return null;

        var candidates = Path.HasExtension(command)
            ? [command]
            : (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(ext => command + ext.ToLowerInvariant())
                .ToArray();

        foreach (var candidate in candidates)
        {
            var appPath = ReadAppPath(Registry.CurrentUser, candidate) ?? ReadAppPath(Registry.LocalMachine, candidate);
            if (appPath != null && File.Exists(appPath)) return appPath;
        }

        var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var directory in directories)
        {
            foreach (var candidate in candidates)
            {
                try
                {
                    var full = Path.Combine(Environment.ExpandEnvironmentVariables(directory.Trim('"')), candidate);
                    if (File.Exists(full)) return full;
                }
                catch (ArgumentException)
                {
                    // Invalid PATH entry: ignore
                }
            }
        }

        return null;
    }

    private static string? ReadAppPath(RegistryKey root, string exeName)
    {
        using var key = root.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exeName}");
        return key?.GetValue(null) is string value ? Expand(value) : null;
    }
}
