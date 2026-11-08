# Contributing

Bug reports, ideas, translations and pull requests are welcome.

## Building

Requirements: Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build                     # build
dotnet test                      # unit tests
dotnet run --project src/HotkeyLauncher

# Portable single-file executable
dotnet publish src/HotkeyLauncher -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

| Folder | Content |
|--------|---------|
| `src/HotkeyLauncher.Core` | Model, settings, launching, search, imports (no UI, unit tested) |
| `src/HotkeyLauncher` | WPF application ([WPF-UI](https://github.com/lepoco/wpfui) Fluent controls, MVVM with [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)) |
| `tests/` | xUnit tests of the core and of the view models |
| `tools/` | Translations (`strings.tsv`) and the script generating the `.resx` files |
| `packaging/` | Scoop, winget and Chocolatey templates (see [packaging/README.md](packaging/README.md)) |
| `docs/` | Logo, screenshots and the demo settings used to take them |

### Files written by the application

| File | Content |
|------|---------|
| `HotkeyLauncher.settings.json` | Settings and shortcuts |
| `HotkeyLauncher.log` | Log (start, errors, shortcuts that could not be registered) |
| `backups\` | The settings file as it was at the start of the last 3 sessions |

They are next to `HotkeyLauncher.exe`, or in `%APPDATA%\HotkeyLauncher` if that folder is read-only or managed by
winget / Chocolatey.

## Translations

All the texts are in [`tools/strings.tsv`](tools/strings.tsv) (one column per language). After a change, regenerate
the resources with `python tools/make_resx.py`.
To add a language: add a column, then declare it in `Localizer.Languages` and in `SatelliteResourceLanguages`
(`Directory.Build.props`).

## Releasing

Every push on `main` is built and tested by the CI. To publish a release, push a version tag:

```powershell
git tag v1.2.3
git push origin v1.2.3
```

The *Release* workflow builds the 4 zip files, `SHA256SUMS.txt`, the package manager manifests, creates the GitHub
release and publishes to Chocolatey and winget. A tag with a suffix (`v1.2.3-beta.1`) creates a pre-release.

## Screenshots and demo animation

Use the demo settings, so that no personal path appears: copy
[`docs/screenshots/demo.json`](docs/screenshots/demo.json) as `HotkeyLauncher.settings.json` next to the executable,
English interface, light theme.

The README animation (`docs/demo.gif`) can be recorded with [ScreenToGif](https://www.screentogif.com/)
(`winget install NickeManarin.ScreenToGif`), free:

- recording area about 1280 × 800, 12 frames per second, **15 to 20 seconds**, keystrokes displayed
  (*Editor → Image → Key strokes*), saved below 5 MB (*Save as → Gif*, resized to 800 px wide if needed);
- storyboard:
  1. empty desktop, press `Ctrl + Alt + N`: Notepad opens (2 s);
  2. press `Win + Alt + Space`, type `calc`, Enter: Calculator opens (3 s);
  3. open HotkeyLauncher, drag a program onto the window, click 2 or 3 keys of the on-screen keyboard
     (free keys are visible), Save (8 s);
  4. press the new shortcut: the program opens (2 s).

Before recording, close all Notepad tabs and set Notepad to *Start a new session* (otherwise it reopens your
previous files on screen), and enable *Do not disturb*. Check every frame for personal information before
publishing. Then replace `docs/demo.gif` (shown at the top of the README).
