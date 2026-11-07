using Microsoft.Win32;

namespace HotkeyLauncher.Core;

/// <summary>
/// "Start with Windows" through the per-user Run registry key (no administrator rights needed).
/// As the application is portable, the registered path is repaired when the executable has been moved.
/// </summary>
public sealed class StartupRegistration(string valueName, string executablePath, string arguments)
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string Command => string.IsNullOrEmpty(arguments) ? $"\"{executablePath}\"" : $"\"{executablePath}\" {arguments}";

    public bool IsEnabled => ReadValue() != null;

    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(valueName, Command);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    /// <summary>Updates the registered command if it points to another location. Returns true if repaired.</summary>
    public bool RepairIfMoved()
    {
        var current = ReadValue();
        if (current == null || string.Equals(current, Command, StringComparison.OrdinalIgnoreCase)) return false;
        Enable();
        return true;
    }

    private string? ReadValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(valueName) as string;
    }
}
