using System.ComponentModel;
using System.Diagnostics;
using HotkeyLauncher.Core.Models;

namespace HotkeyLauncher.Core.Launching;

public static class Launcher
{
    /// <summary>Win32 error returned when the user answers "No" to the UAC prompt.</summary>
    public const int ErrorCancelled = 1223;

    public static ProcessStartInfo BuildStartInfo(HotkeyEntry entry)
    {
        var isUrl = TargetResolver.IsUrl(entry.Target);
        var fileName = isUrl ? entry.Target.Trim() : TargetResolver.Expand(entry.Target);
        var kind = TargetResolver.Classify(entry.Target);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = true,
            WindowStyle = entry.WindowState switch
            {
                LaunchWindowState.Minimized => ProcessWindowStyle.Minimized,
                LaunchWindowState.Maximized => ProcessWindowStyle.Maximized,
                _ => ProcessWindowStyle.Normal,
            },
        };

        if (!string.IsNullOrWhiteSpace(entry.Arguments)) startInfo.Arguments = Environment.ExpandEnvironmentVariables(entry.Arguments.Trim());

        if (!string.IsNullOrWhiteSpace(entry.WorkingDirectory))
        {
            startInfo.WorkingDirectory = TargetResolver.Expand(entry.WorkingDirectory);
        }
        else if (kind is TargetKind.Application or TargetKind.File && Path.IsPathRooted(fileName))
        {
            // Like a Windows shortcut: start in the program's own folder
            startInfo.WorkingDirectory = Path.GetDirectoryName(fileName) ?? string.Empty;
        }

        if (entry.RunAsAdministrator && kind is TargetKind.Application or TargetKind.Command or TargetKind.NotFound)
        {
            startInfo.Verb = "runas";
        }

        return startInfo;
    }

    /// <summary>Launches the entry. Returns false when the user cancelled the elevation prompt.</summary>
    /// <exception cref="Win32Exception">The target cannot be started.</exception>
    public static bool Launch(HotkeyEntry entry)
    {
        try
        {
            Process.Start(BuildStartInfo(entry))?.Dispose();
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return false;
        }
    }
}
