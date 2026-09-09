# Pocket Atlas handoff for Fillomino Go

This folder preserves the Pocket Atlas visual direction originally implemented in Shikaku Go. It is intended as a portable design and implementation reference for Fillomino Go.

## What is included

- `PortableAssets/Art/UI/Atlas/Pip`: the six-pose transparent Pip sprite sheet and Unity slicing metadata.
- `PortableAssets/Art/UI/Atlas/Motifs`: six seamless district motifs: contours, orchard dots, streets, water lines, trail marks, and survey hatching.
- `PortableAssets/Art/Logos`: the opaque Pocket Atlas app icon.
- `ReferenceImplementation`: the shared Atlas stylesheet, theme configuration, theme builder, populated theme asset, and pooled district-decoration renderer.
- `IntegrationSnapshots`: the exact Home, Settings, Gameplay HUD, board, cell, and UXML files at handoff time.
- `ShikakuGo-PocketAtlas-tracked-changes.patch`: the tracked-file diff from the Shikaku Go implementation.

## Important Fillomino adaptation

Do not port Shikaku's strict rectangle validation, one-clue rule, or `width × height = area` preview. Fillomino regions may have any connected shape.

For Fillomino Go, retain the Atlas theme but adapt the region visual descriptor to contain a cell mask or boundary path. Render one continuous motif in board coordinates and clip it to every cell belonging to the region. This keeps the map texture continuous across irregular shapes without repeating decoration independently inside each cell.

A valid Fillomino placement should continue to use Fillomino's existing rules: connected equal-number cells, final region size matching the number, and no orthogonally touching separate regions with the same number. Decorative motifs must never communicate correctness or clue value on their own.

## Reusable design decisions

- Warm parchment and charcoal-paper light/dark foundations.
- Ink, brass, moss, lake blue, route red, and muted slate palette.
- Pip appears only for welcome, teaching, inspection/error, hints, and celebration.
- One restrained motif per completed region, selected deterministically from stable region data.
- Faint internal grid lines with a stronger region boundary.
- Short ink-settle feedback and a one-shot route sweep; both disabled by Reduce Motion.
- One-column destination cards on phones and two columns on portrait tablets.
- Shared paper-sheet dialogs and preserved offline, ad, privacy, purchase, and entitlement behavior.

## Generated-art specifications

- Pip sheet: `1536 × 1024`, RGBA PNG, transparent background, six `512 × 512` poses in a 3 × 2 grid.
- App icon: `1254 × 1254`, opaque RGB PNG.
- Motifs: six programmatically generated repeatable `64 × 64` RGBA PNG textures.

## Recommended Fillomino port order

1. Copy `PortableAssets` and recreate the theme configuration in the Fillomino project.
2. Port the shared stylesheet selectively while preserving Fillomino controller query names.
3. Replace rectangle bounds in `AtlasRegionDecorationLayer` with a region cell mask or generated boundary mesh.
4. Hash stable region cells/value to choose palette and motif deterministically.
5. Add Pip only to guide and feedback surfaces.
6. Verify arbitrary shapes, split/merged regions, save reloads, dark mode, labels, reduced motion, and allocation-free dragging.

## Verification at handoff

The Shikaku reference implementation imported and compiled in Unity 6000.6.0f1. The focused Shikaku/Atlas fixture passed 9 of 9 tests. The broader suite passed 55 of 58; the three unrelated failures require production iOS AdMob/Firebase credentials and `GoogleService-Info.plist`.
