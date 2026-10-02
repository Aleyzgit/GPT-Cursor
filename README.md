# GPT Cursor

A Windows cursor overlay inspired by the ChatGPT/Codex computer-use cursor, without the blue glow. English and German UI. Windows 10 (1809+) / Windows 11, x64.

[Download the latest setup](https://github.com/Aleyzgit/GPT-Cursor/releases/latest)

## Install and update

Run `GPT-Cursor-Setup-<version>.exe`. The installer includes the .NET runtime and installs for the current user. Windows Settings → Installed apps → GPT Cursor removes it. Startup and desktop shortcuts are optional.

In **System**, use **Check for updates** and **Download and install**. Automatic checks run shortly after launch and every six hours while enabled. Only newer stable GitHub releases are offered. The setup is downloaded over HTTPS from this repository and verified against the SHA-256 digest returned by GitHub before launch. Installation is initiated by the user; no silent background replacement. Existing installed settings and startup choices are retained.

Versions before 1.2.0 need one manual installation of the new setup to gain the updater. Updating from a portable copy installs the normal edition; existing installed preferences take priority, otherwise portable preferences are imported.

## Cursor options

- Output rate: 60–360 FPS, 240 FPS by default.
- Independent rotation, stretch, squash, after-wobble, left/right click bounce and click-ring switches.
- **Point in movement direction** follows a short movement trail instead of individual rasterized pixels. It supports arbitrary angles, ignores tiny resting jitter and holds the displayed heading when stopped. Stretch remains independent.
- Position and animation smoothing can be enabled separately, each with Sine easing, Soft spring or Responsive.
- Configurable global shortcut (default Ctrl + Alt + C), optional startup in the tray, English/German language selection.

## Compatibility

MouseX can stay running. The overlay uses `MagShowSystemCursor` and does not replace system cursor images.

Windows shell surfaces such as Start and the secure UAC desktop use the native Windows/MouseX cursor. A normal overlay cannot display its own cursor on the protected UAC desktop. No Windows security policies or certificate stores are changed.

Fullscreen applications use their own cursor by default. Windowed games with software cursors can be excluded by executable name under **System**, for example `game.exe; javaw.exe`. Automatic detection cannot cover every custom game cursor. There is no game injection or anti-cheat integration.

Visual position smoothing introduces a small following delay. Clicks and dragging synchronize the overlay to the actual mouse position; Windows input coordinates are never changed.

## Settings

Installed: `%LOCALAPPDATA%\GPTCursor\settings.json`. Uninstall keeps preferences for a later reinstall. Portable: `settings.json` beside the executable. Preferences and local test/build output are not tracked in Git.

## Build and test

Requires .NET 10 SDK; installer builds also require Inno Setup 6.

```powershell
dotnet build -c Release
$p = Start-Process '.\bin\Release\net10.0-windows\GPT Cursor.exe' -ArgumentList '--self-test test-output' -Wait -PassThru
if ($p.ExitCode -ne 0) { throw 'Tests failed' }
.\installer\Build-Setup.ps1 -Compiler 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

`--frame-test <folder>` measures application update timing, not monitor scanout. `--overlay-test <folder>` checks rendering and resource usage. `--smoke-test <file>` checks activation/restoration in an interactive Windows session. `--quit` gracefully closes the current instance; `--autostart` activates in the tray.

`installer/Test-Setup.ps1` performs a temporary install/autostart/uninstall test and refuses to overwrite an existing installation or startup entry.

## Releases

The project version in `GPTCursor.csproj` controls the setup filename. Commit validated changes, update the version for a release, then push a matching `vMAJOR.MINOR.PATCH` tag. GitHub Actions builds and tests the application, packages the self-contained installer, and publishes the installer and SHA-256 file as a GitHub Release. The in-app updater reads these releases. A commit alone does not publish an installable update.

The app and setup are currently unsigned. Windows may show an unknown-publisher prompt.

## Attribution

This is an unofficial personal utility. The cursor bitmap was extracted from the locally installed OpenAI desktop app; no ownership or open-source license for that asset is claimed. See [SOURCES.md](SOURCES.md) for precise provenance and differences. The app's original agent paths are adapted to physical mouse movement; the added click animations, smoothing, direction tracking and updater are independent implementations.
