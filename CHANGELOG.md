# Changelog

## 1.2.0

- Fix direction locking to eight angles by estimating heading over a short travelled path.
- Hold the displayed heading at rest and reject one-pixel resting jitter.
- Add automatic and manual stable-release checks, verified installer downloads and user-initiated updates.
- Add GitHub Actions builds and tag-based installer releases.
- Use the project version as the single source for installer filenames.

## 1.1.0

- Per-user installer/uninstaller with bundled .NET runtime.
- Optional Windows startup and direction-facing cursor mode.
- Native-cursor fallback on protected shell surfaces, fullscreen pause and app exclusions.
- Configurable shortcut, English/German UI, click effects and independent motion smoothing.
