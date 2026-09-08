# Shikaku Go migration notes

This copy has completed a first-pass cleanup and product rename.

## Reusable systems retained

- Unity UI shell, scenes, themes, audio, general art, save/settings infrastructure
- Menus, progression modes, stores, ads, analytics, privacy, notifications, and platform tooling
- Puzzle models, board presentation, and gameplay code as migration references

## Still requires Shikaku-specific work

- Replace the legacy region-growth puzzle rules in `Assets/Scripts/Logic`, `Assets/Scripts/UI/BoardController.cs`, `Assets/Scripts/UI/CellView.cs`, and related tutorial/hint code with rectangle-partition rules.
- Add newly baked Shikaku puzzle packs under `Assets/Resources/Puzzles`.
- Create new app icons and splash branding.
- Provision new Firebase, AdMob, Google Play Games, and App Store records, then replace every `REPLACE_WITH_SHIKAKU_*` placeholder.
- Reconnect Unity services only after creating a separate Shikaku cloud project.

## Recoverable quarantine

`.cleanup-quarantine` contains the copied Fillomino puzzle packs, service configuration, generated Android libraries, and old app branding. It is ignored by version control and is not imported by Unity. Delete it only after the Shikaku replacements are verified.