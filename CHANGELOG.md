# Changelog

## 1.4.0

- Add optional "Keep small while pressed" for left/right clicks, with a smooth return to normal size on release.
- Respect individual button switches, support holding both buttons, and keep click rings independent.

## 1.3.0

- Add a direction style that follows movement and smoothly returns to the original orientation at rest.
- Keep the original animation and the direction-holding style as separate dropdown choices.
- Preserve stretch, squash, click effects and optional after-wobble; retain existing direction preferences.

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
