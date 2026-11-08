$ErrorActionPreference = 'Stop'
$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

# Self-contained build: nothing else to install. x64 also runs on Windows on ARM (emulation).
$packageArgs = @{
  packageName    = $env:ChocolateyPackageName
  unzipLocation  = $toolsDir
  url64bit       = 'https://github.com/sybaris/HotkeyLauncher/releases/download/vx.y.z/HotkeyLauncher-x.y.z-win-x64-portable.zip'
  checksum64     = 'SHA256_X64'
  checksumType64 = 'sha256'
}
Install-ChocolateyZipPackage @packageArgs

# Windows application: the shim must not wait for it to exit
New-Item -Path (Join-Path $toolsDir 'HotkeyLauncher.exe.gui') -ItemType File -Force | Out-Null

$programs = [Environment]::GetFolderPath('CommonPrograms')
Install-ChocolateyShortcut -ShortcutFilePath (Join-Path $programs 'HotkeyLauncher.lnk') -TargetPath (Join-Path $toolsDir 'HotkeyLauncher.exe')
