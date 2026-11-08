$ErrorActionPreference = 'Stop'

$shortcut = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'HotkeyLauncher.lnk'
if (Test-Path $shortcut) { Remove-Item $shortcut -Force }

# "Start with Windows" entry of the current user (the settings in %APPDATA%\HotkeyLauncher are kept)
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'HotkeyLauncher' -ErrorAction SilentlyContinue
