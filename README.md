<p align="center">
  <img src="docs/logo.png" width="112" alt="HotkeyLauncher logo">
</p>

<h1 align="center">HotkeyLauncher</h1>

<h3 align="center">Your apps, folders and web sites, one keystroke away.</h3>

<p align="center">
  Press <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>N</kbd> anywhere in Windows and Notepad opens. That's it.<br>
  Free, open source, portable, no account, no ads, no telemetry.
</p>

<p align="center">
  <a href="https://github.com/sybaris/HotkeyLauncher/releases/latest"><img src="https://img.shields.io/badge/Download-Windows%2010%20%2F%2011-0078D4?style=for-the-badge&logo=windows&logoColor=white" alt="Download for Windows 10 / 11"></a>
</p>

<p align="center">
  <a href="https://github.com/sybaris/HotkeyLauncher/releases/latest"><img src="https://img.shields.io/github/v/release/sybaris/HotkeyLauncher?label=version" alt="Latest version"></a>
  <a href="https://github.com/sybaris/HotkeyLauncher/releases"><img src="https://img.shields.io/github/downloads/sybaris/HotkeyLauncher/total?label=downloads" alt="Downloads"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/sybaris/HotkeyLauncher" alt="MIT license"></a>
  <a href="#français"><img src="https://img.shields.io/badge/lire%20en-fran%C3%A7ais-blue" alt="Lire en français"></a>
</p>

<p align="center">
  <img src="docs/demo.gif" width="760" alt="HotkeyLauncher in action: choosing a shortcut on the on-screen keyboard, then launching the Calculator with Ctrl + Alt + C">
</p>

## Why HotkeyLauncher?

You open the same 10 programs, folders and sites all day long. Each time: Start menu, type, scroll, click…
HotkeyLauncher gives each of them **its own keyboard shortcut**, working from any application.

- ⚡ **Instant** – one key combination, the app is there. Already open? Its window can simply come to the front.
- 🎯 **No more "is this shortcut free?"** – an on-screen keyboard shows which keys are free, already used by you,
  or taken by Windows, and suggests free combinations.
- 🔎 **Forgot a shortcut?** – press <kbd>Win</kbd> + <kbd>Alt</kbd> + <kbd>Space</kbd>, type 2 or 3 letters, Enter.
- 🪶 **Light and discreet** – lives in the notification area, no installation needed, no data collected.

## Install in 30 seconds

**Option 1 – download** the [latest version](https://github.com/sybaris/HotkeyLauncher/releases/latest)
(`HotkeyLauncher-x.y.z-win-x64-portable.zip`), unzip it anywhere (for instance `C:\Tools\HotkeyLauncher`)
and run `HotkeyLauncher.exe`. Nothing to install, nothing else to download.

**Option 2 – with a package manager:**

```powershell
winget install Sybaris.HotkeyLauncher
choco install hotkeylauncher
scoop bucket add sybaris https://github.com/sybaris/scoop-bucket; scoop install hotkeylauncher
```

> The program is not signed (signing certificates are expensive for a free project), so on first launch Windows
> SmartScreen may display a warning: click *More info* → *Run anyway*. The source code is public and every release
> is built by GitHub from that code.

## Get started

1. Click **Add**, choose what to launch – or simply **drag a program, a file or a folder** onto the window.
2. Click in the shortcut box and press your combination – or click the keys of the on-screen keyboard.
3. Save. The shortcut works immediately, from any application.

Closing the window keeps HotkeyLauncher running in the notification area (next to the clock).
Tick *Start with Windows* in the settings and you're done.

## Features

<table>
<tr>
<td width="50%"><img src="docs/screenshots/editor.png" alt="Shortcut editor with the on-screen keyboard"></td>
<td>

### Pick a shortcut without guessing

The on-screen keyboard follows your layout (AZERTY, QWERTY, QWERTZ…) and colors each key:
**free**, **used by one of your shortcuts**, or **taken by Windows / another program**.
Free combinations are suggested in one click.

</td>
</tr>
<tr>
<td>

### Find anything in 2 keystrokes

The quick launcher searches all your shortcuts as you type: `vsc` finds *Visual Studio Code*.
The notification-area icon also offers a menu of all your shortcuts, with a search box.

</td>
<td width="50%"><img src="docs/screenshots/quick-launcher.png" alt="Quick launcher"></td>
</tr>
</table>

**Launch anything**
- Programs (with arguments and start-in folder), documents, folders, web sites, links (`mailto:`, `ms-settings:`…)
  and scripts (`.ps1`, `.bat`, `.cmd`, `.vbs`).
- Run **as administrator** or **hidden** (handy for scripts).
- **Window position**: choose the screen, centered or on a half of the screen.

**Stay organized**
- **Groups**, drag & drop reordering, sort by name, most used or recently used.
- **Usage statistics** on each shortcut.
- **Undo** (<kbd>Ctrl</kbd> + <kbd>Z</kbd>) after a deletion, **automatic backups**, **export / import** to another PC.

**Feels at home in Windows 11**
- Fluent design, **light / dark theme**.
- In **English, French, Spanish and German**.
- **Start with Windows** without administrator rights, update notifications (can be disabled).

<p align="center">
  <img src="docs/screenshots/main.png" width="49%" alt="Main window: shortcuts by group, with usage statistics">
  <img src="docs/screenshots/settings.png" width="49%" alt="Settings">
</p>

## FAQ

<details>
<summary><b>Is it really free?</b></summary>

Yes, and open source ([MIT license](LICENSE)): no paid version, no ads, no account, no data collection.
The only network access is an optional daily check for a new version on GitHub.
</details>

<details>
<summary><b>Where are my settings? How do I uninstall it?</b></summary>

The settings are in `HotkeyLauncher.settings.json`, next to `HotkeyLauncher.exe` (or in `%APPDATA%\HotkeyLauncher`
when installed with winget / Chocolatey, or when the folder is read-only). The exact location is shown in *Settings*.
To uninstall the zip version: untick *Start with Windows*, exit from the tray icon and delete the folder.
</details>

<details>
<summary><b>How is it different from PowerToys Run or the Start menu?</b></summary>

Those make you search each time. HotkeyLauncher is about **direct shortcuts**: one combination, always the same,
for the things you use the most – with a tool that tells you which combinations are free. It is a single small
program, without installation. (It also has a search, the quick launcher, for everything else.)
</details>

<details>
<summary><b>A shortcut doesn't work.</b></summary>

Another program probably owns that combination: Windows lets only one program register it. HotkeyLauncher then tells you
which shortcuts could not be enabled; pick another one with the on-screen keyboard. Details are in the log file `HotkeyLauncher.log`.
</details>

<details>
<summary><b>I used RapidApp.</b></summary>

HotkeyLauncher is the successor of [RapidApp](https://sourceforge.net/projects/rapidapp/) (2003 – 2005).
Import your `RapidApp.ini` from *Settings → Advanced → Import from RapidApp*.
</details>

<details>
<summary><b>Command line options</b></summary>

| Option | Effect |
|--------|--------|
| *(none)* | Opens the main window (or brings the running instance to the front) |
| `/setup` | Same, kept for RapidApp compatibility |
| `--tray` | Starts hidden in the notification area (used by *Start with Windows*) |
| `/?`, `--help` | Displays the help |
</details>

## Like it?

⭐ **Star the project** on GitHub, it really helps other people find it. Ideas and bugs are welcome in the
[issues](https://github.com/sybaris/HotkeyLauncher/issues), translations and code in pull requests
(see [CONTRIBUTING.md](CONTRIBUTING.md)).

> This project was 99% generated by [Claude Opus 5.5](https://www.anthropic.com/claude) using
> [Claude Code](https://www.anthropic.com/claude-code).

[MIT](LICENSE) © Jean-Pierre Planas

---

## Français

<h3>Vos applications, dossiers et sites web, à une touche de distance.</h3>

Appuyez sur <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>N</kbd> n'importe où dans Windows et le Bloc-notes s'ouvre.
Gratuit, open source, portable, sans compte, sans publicité, sans télémétrie.

**Pourquoi HotkeyLauncher ?**
- ⚡ **Instantané** : une combinaison de touches et l'application est là. Déjà ouverte ? Sa fenêtre peut simplement revenir au premier plan.
- 🎯 **Fini le « ce raccourci est-il libre ? »** : un clavier visuel montre les touches libres, déjà utilisées ou
  prises par Windows, et propose des combinaisons libres.
- 🔎 **Un raccourci oublié ?** <kbd>Win</kbd> + <kbd>Alt</kbd> + <kbd>Espace</kbd>, 2 ou 3 lettres, Entrée.
- 🪶 **Léger et discret** : dans la zone de notification, sans installation, aucune donnée collectée.

**Installation** : téléchargez la [dernière version](https://github.com/sybaris/HotkeyLauncher/releases/latest)
(fichier `win-x64-portable.zip`), décompressez-la où vous voulez et lancez `HotkeyLauncher.exe`.
Ou : `winget install Sybaris.HotkeyLauncher`, `choco install hotkeylauncher`, ou
`scoop bucket add sybaris https://github.com/sybaris/scoop-bucket` puis `scoop install hotkeylauncher`.

> L'exécutable n'est pas signé : au premier lancement, Windows SmartScreen peut afficher un avertissement.
> Cliquez sur *Informations complémentaires* → *Exécuter quand même*.

**Démarrer** : cliquez sur **Ajouter** (ou glissez un programme, un fichier ou un dossier sur la fenêtre), appuyez sur
la combinaison voulue ou cliquez sur les touches du clavier visuel, enregistrez. C'est tout.

**Et aussi** : scripts, exécution en administrateur ou masquée, position de la fenêtre, groupes, glisser-déposer,
statistiques d'utilisation, annulation (<kbd>Ctrl</kbd> + <kbd>Z</kbd>), sauvegardes automatiques, export / import,
thème clair / sombre, démarrage avec Windows, interface en français, anglais, espagnol et allemand.

**Anciens utilisateurs de [RapidApp](https://sourceforge.net/projects/rapidapp/)** : importez votre `RapidApp.ini`
depuis *Paramètres → Avancé → Importer depuis RapidApp*.

⭐ Si le logiciel vous plaît, **une étoile sur GitHub** l'aide à se faire connaître.
