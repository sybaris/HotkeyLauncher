# Packaging

HotkeyLauncher is a portable application: the packages below simply download the release zip and unzip it.
Each GitHub release contains a `SHA256SUMS.txt` file and a ready-to-use Scoop manifest (`hotkeylauncher.json`).

## Scoop

Scoop installs portable apps from "buckets" (git repositories of JSON manifests).

1. Create a public repository, for example `sybaris/scoop-bucket`, with a `bucket/` folder.
2. Copy the `hotkeylauncher.json` attached to the latest release into `bucket/`, commit and push.
   (`scoop/hotkeylauncher.json` in this folder is the template it is generated from: its `checkver`/`autoupdate`
   sections let `scoop` maintainers' tooling update it automatically.)
3. Users then install with:

   ```powershell
   scoop bucket add sybaris https://github.com/sybaris/scoop-bucket
   scoop install hotkeylauncher
   ```

Settings are kept across updates thanks to `persist` (the settings file, log and backups live in Scoop's `persist` folder).

## winget

winget packages are submitted by pull request to [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs).
HotkeyLauncher uses the *zip + portable* installer type: winget unzips it and adds `hotkeylauncher` to the PATH.

The easiest way is [wingetcreate](https://github.com/microsoft/winget-create):

```powershell
winget install Microsoft.WingetCreate
# First version: answers the questions interactively, then opens the pull request
wingetcreate new https://github.com/sybaris/HotkeyLauncher/releases/download/v1.0.0/HotkeyLauncher-1.0.0-win-x64-portable.zip
# Next versions
wingetcreate update Sybaris.HotkeyLauncher --version 1.1.0 --urls https://github.com/sybaris/HotkeyLauncher/releases/download/v1.1.0/HotkeyLauncher-1.1.0-win-x64-portable.zip --submit
```

`winget/` contains a reference manifest (version 1.10 schema) showing the expected values; replace `x.y.z` and the
SHA256 (from `SHA256SUMS.txt`) if you prefer writing it by hand.

Users then install with `winget install Sybaris.HotkeyLauncher`.
