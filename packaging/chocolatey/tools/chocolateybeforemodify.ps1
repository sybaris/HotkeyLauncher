# Upgrade / uninstall: the running executable would lock its files
Get-Process -Name 'HotkeyLauncher' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
