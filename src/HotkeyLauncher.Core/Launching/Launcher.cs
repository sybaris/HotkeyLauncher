using System.ComponentModel;
using System.Diagnostics;
using HotkeyLauncher.Core.Models;

namespace HotkeyLauncher.Core.Launching;

public enum LaunchResult
{
    Started,
    /// <summary>The program was already running: its window has been brought to the front.</summary>
    Activated,
    /// <summary>The user answered "No" to the administrator (UAC) prompt.</summary>
    Cancelled,
}

public static class Launcher
{
    /// <summary>Win32 error returned when the user answers "No" to the UAC prompt.</summary>
    public const int ErrorCancelled = 1223;

    public static ProcessStartInfo BuildStartInfo(HotkeyEntry entry)
    {
        var isUrl = TargetResolver.IsUrl(entry.Target);
        var fileName = isUrl ? entry.Target.Trim() : TargetResolver.Expand(entry.Target);
        var kind = TargetResolver.Classify(entry.Target);
        var arguments = string.IsNullOrWhiteSpace(entry.Arguments) ? string.Empty : Environment.ExpandEnvironmentVariables(entry.Arguments.Trim());

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = true,
            WindowStyle = entry.RunHidden
                ? ProcessWindowStyle.Hidden
                : entry.WindowState switch
                {
                    LaunchWindowState.Minimized => ProcessWindowStyle.Minimized,
                    LaunchWindowState.Maximized => ProcessWindowStyle.Maximized,
                    _ => ProcessWindowStyle.Normal,
                },
        };

        // PowerShell scripts are opened by Notepad by default: run them with PowerShell
        if (kind == TargetKind.Script && TargetResolver.IsPowerShellScript(fileName))
        {
            startInfo.FileName = "powershell.exe";
            var hidden = entry.RunHidden ? "-WindowStyle Hidden " : string.Empty;
            arguments = $"-NoProfile -ExecutionPolicy Bypass {hidden}-File \"{fileName}\" {arguments}".TrimEnd();
        }

        if (arguments.Length > 0) startInfo.Arguments = arguments;

        if (!string.IsNullOrWhiteSpace(entry.WorkingDirectory))
        {
            startInfo.WorkingDirectory = TargetResolver.Expand(entry.WorkingDirectory);
        }
        else if (kind is TargetKind.Application or TargetKind.Script or TargetKind.File && Path.IsPathRooted(fileName))
        {
            // Like a Windows shortcut: start in the program's own folder
            startInfo.WorkingDirectory = Path.GetDirectoryName(fileName) ?? string.Empty;
        }

        if (entry.RunAsAdministrator && kind is TargetKind.Application or TargetKind.Script or TargetKind.Command or TargetKind.NotFound)
        {
            startInfo.Verb = "runas";
        }

        return startInfo;
    }

    /// <summary>True when "bring to front if already running" makes sense for this target.</summary>
    public static bool CanBringToFront(string target) =>
        TargetResolver.Classify(target) is TargetKind.Application or TargetKind.Command;

    /// <summary>Launches the entry (or activates its window, depending on its options).</summary>
    /// <exception cref="Win32Exception">The target cannot be started.</exception>
    public static LaunchResult Launch(HotkeyEntry entry)
    {
        if (entry.IfRunning == RunningBehavior.BringToFront && CanBringToFront(entry.Target) && WindowManager.TryActivateExisting(entry.Target))
        {
            return LaunchResult.Activated;
        }

        Process? process;
        try
        {
            process = Process.Start(BuildStartInfo(entry));
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return LaunchResult.Cancelled;
        }

        if (process != null && entry.NeedsPlacement && !entry.RunHidden)
        {
            _ = WindowManager.PlaceWhenReadyAsync(process, entry);
        }
        else
        {
            process?.Dispose();
        }

        return LaunchResult.Started;
    }
}
