# Packaging

HotkeyLauncher is a portable application: the packages below simply download the self-contained release zip.
The package name is `hotkeylauncher` everywhere; the winget identifier is `Sybaris.HotkeyLauncher`.

| Channel | Users install with | Settings are stored in |
|---------|--------------------|------------------------|
| Scoop | `scoop bucket add sybaris https://github.com/sybaris/scoop-bucket` then `scoop install hotkeylauncher` | Scoop's `persist` folder (kept across updates) |
| winget | `winget install Sybaris.HotkeyLauncher` | `%APPDATA%\HotkeyLauncher` |
| Chocolatey | `choco install hotkeylauncher` | `%APPDATA%\HotkeyLauncher` |

The application detects a winget or Chocolatey folder (`SettingsStore.IsManagedByPackageManager`) and then stores
its settings in `%APPDATA%`, because those folders are replaced by updates.

## What the release workflow does

For every stable tag (`v1.2.3`, not `v1.2.3-beta.1`), `.github/workflows/release.yml`:

1. attaches to the GitHub release the zips, `SHA256SUMS.txt`, the Scoop manifest `hotkeylauncher.json` and the
   Chocolatey package `hotkeylauncher.x.y.z.nupkg`;
2. **pushes the Chocolatey package** if the `CHOCOLATEY_API_KEY` secret exists;
3. **opens the winget pull request** if the `WINGET_TOKEN` secret exists.

Without these secrets, the steps are skipped and the release is still created.
The templates (`x.y.z`, `SHA256_X64`, `SHA256_ARM64` are replaced) are in `scoop/`, `winget/` and `chocolatey/`.

## Scoop (one-time setup)

Scoop installs apps from "buckets", git repositories of JSON manifests.

1. Create the bucket from Scoop's template (it contains the *Excavator* workflow that updates manifests automatically
   thanks to `checkver` / `autoupdate`):

   ```powershell
   gh repo create sybaris/scoop-bucket --public --template ScoopInstaller/BucketTemplate --clone
   ```

2. Put the `hotkeylauncher.json` attached to the latest release in `bucket/`, replace the placeholder in
   `bin/auto-pr.ps1` by `sybaris/scoop-bucket:master` (the template's default branch is `master`), commit and push.
3. Check that the bucket repository settings, *Actions → General → Actions permissions*, are on *Allow all actions
   and reusable workflows* (the default). The Excavator workflow declares `contents: write` itself: the default
   read-only workflow permissions are fine.
4. Optional: add the `scoop-bucket` topic to the repository so that the bucket is listed on https://scoop.sh.

## winget

The first version must be submitted once; the next ones are automatic with the `WINGET_TOKEN` secret.

1. Create a *classic* personal access token on the account that will open the pull requests
   (*Settings → Developer settings → Tokens (classic)*, scope `public_repo`).
2. First submission (manifests filled the same way as the workflow does, in `packaging-out/winget/x.y.z`):

   ```powershell
   winget install Microsoft.WingetCreate
   wingetcreate submit --token <token> packaging-out\winget\1.0.0
   ```

   Microsoft's bots validate the package (download, antivirus scan, installation) and a moderator merges the pull
   request, usually within a few days.
3. Add the token as the `WINGET_TOKEN` secret of the HotkeyLauncher repository
   (*Settings → Secrets and variables → Actions*).

## Chocolatey

1. Create an account on [community.chocolatey.org](https://community.chocolatey.org/account/Register) and copy your
   API key (*Account → API Keys*).
2. Add it as the `CHOCOLATEY_API_KEY` secret of the HotkeyLauncher repository.
3. For the current version, push the package once:

   ```powershell
   choco push hotkeylauncher.1.0.0.nupkg --source https://push.chocolatey.org/ --api-key <key>
   ```

Every version goes through automatic checks then, for the first versions of a package, a human moderation
(from a few days to a few weeks). Testing the package locally before (administrator prompt):

```powershell
choco install hotkeylauncher --source .\artifacts -y
choco uninstall hotkeylauncher -y
```
