# Modern blueprint UI

Implemented September 13, 2026. Adventure's map, chapter screens, art, and controller behavior are preserved.

## What changed

- An opt-in light/dark style sheet replaces the busy paper treatment on Home, Daily, Time Trial, Free Play, Settings, Shop, privacy screens, gameplay, and dialogs.
- Backgrounds use faint retained vector grids. Cards have opaque surfaces, restrained outlines, readable text, and consistent spacing.
- Home has a full-width Adventure entry, equal Free Play/Time Trial cards, a Daily completion status, and compact secondary actions.
- Daily uses an intrinsic-height calendar with consistent day cells and a scrollable body. Time Trial uses three consistent horizontal cards.
- Free Play uses readable pack progress, grid-size cards, and clear next/completed/locked level states.
- Gameplay uses a compact header, labeled controls, a readable rewarded-ad badge, and a larger, calmer board. Light/dark board colors live in BlueprintThemeAssets.
- Settings and privacy controls use shared surfaces. Shop includes Restore purchases through the existing store service.
- Dialogs and tutorials use the same typography and surfaces. Rivet's existing expressions are visible again in tutorials and dialogs, including solved results. Adventure results show the animated building construction and floor-completion caption, without Rivet in that panel.

## Implementation

Assets/UI/Themes/ModernBlueprint.uss is loaded after the existing sheets and scoped to .modern-ui roots. Explicit specificity prevents older screen overrides from leaking in without rewriting the existing Adventure styles.

Assets/Scripts/UI/ModernBlueprintLayout.cs installs the vector grid and applies device-safe padding and back-button positions to opted-in menu pages. Scrollable content supports shorter displays.

Existing UXML names and controller bindings are preserved. The Adventure UXML subtree was compared against the working-tree baseline and was structurally unchanged. Existing user edits were retained.

## Verification

- Unity 6000.6 application script compilation passed.
- Shikaku.Tests.ShikakuPuzzleModelTests passed: 10 passed, 0 failed. See puzzle-tests.xml.
- Actual UXML and USS were rendered in an isolated Unity preview project: 14 views/states × 2 themes × 3 panel sizes = 84 captures.
- Panel sizes: 1080 × 2340, 960 × 1700, and 1600 × 2100.
- Automated preview checks covered calendar containment (including six-row months), card horizontal bounds, and absence of modern grid elements under Adventure.
- Visual review corrected the legacy Home width cap, fixed calendar height, Settings colors, and Free Play size-card flow.
- git diff --check passed.

Captures use representative preview data; the gameplay preview uses a representative board rather than a running device session. They verify UI rendering, not live purchases, rewarded ads, touch interaction, or physical-device safe areas. Those flows still need an on-device smoke test.

## Character restoration verification

The restoration was checked separately in 24 Unity renders: standard solved result, Adventure building result, tutorial, and welcome screen, each in two themes at three panel sizes. Assertions verified that the actual BuildingView animation starts and finishes, Adventure results hide Rivet, other results and tutorials resolve Rivet sprites, and result/tutorial cards fit the viewport. Representative building progress was supplied without reading or changing player saves.

See [Character plan](CharacterPlan.md) for the proposed next visual pass.

## Character pass

The character proposal is implemented. Home and page headers use original retained vector illustrations: a city elevation, plan sheets, stopwatch, calendar, drafting tools, and lightbulb. Warm paper and ink-blue surfaces retain readable light/dark contrast.

Rivet changes expression through the tutorial and briefly celebrates non-Adventure results. Approval checks draw in, and saved personal bests receive a gold badge. Adventure results use the building alone. The existing Reduce Motion preference disables the new motion, and scheduled effects reset on detachment.

Validation: game script compilation passed; 102 Unity captures covered 17 states in two themes and three sizes. Additional runtime preview assertions checked animation movement and completion, expression changes, reduced motion (including changing it during an animation), detachment cleanup, duplicate initialization, and Adventure badge exclusion. Dark illustration color inheritance was corrected during visual review.

The captures in the index have been refreshed for these changed views. The gameplay board image and puzzle-model test report are from the earlier overhaul. Live device touch and platform service flows are not covered by these renders.

## Preview index

| Screen | Light | Dark |
|---|---|---|
| Home | [Preview](safe-area-light.png) | [Preview](safe-area-dark.png) |
| Daily | [Preview](daily-screen-light.png) | [Preview](daily-screen-dark.png) |
| Time Trial | [Preview](time-trial-screen-light.png) | [Preview](time-trial-screen-dark.png) |
| Free Play collections | [Preview](free-play-screen-light.png) | [Preview](free-play-screen-dark.png) |
| Grid sizes | [Preview](free-play-size-light.png) | [Preview](free-play-size-dark.png) |
| Levels | [Preview](free-play-level-light.png) | [Preview](free-play-level-dark.png) |
| Gameplay | [Preview](gameplay-light.png) | [Preview](gameplay-dark.png) |
| Settings | [Preview](settings-screen-light.png) | [Preview](settings-screen-dark.png) |
| Shop | [Preview](shop-screen-light.png) | [Preview](shop-screen-dark.png) |
| Daily difficulty | [Preview](daily-modal-light.png) | [Preview](daily-modal-dark.png) |
| Adventure result | [Preview](building-solved-light.png) | [Preview](building-solved-dark.png) |
| Tutorial | [Preview](tutorial-light.png) | [Preview](tutorial-dark.png) |
| Personal best | [Preview](personal-best-light.png) | [Preview](personal-best-dark.png) |
| Puzzle result | [Preview](solved-modal-light.png) | [Preview](solved-modal-dark.png) |
| Welcome | [Preview](privacy-welcome-screen-light.png) | [Preview](privacy-welcome-screen-dark.png) |
| Privacy | [Preview](privacy-settings-light.png) | [Preview](privacy-settings-dark.png) |


## Current floorplan menus

See [Pass 5](FloorplanPass5/README.md) for the current furnished-floorplan menu palette, illustrations, and previews. The older captures above preserve the earlier blueprint iteration.


## Shikaku City branding

See [Shikaku City](ShikakuCity/README.md) for the latest main-menu logo, wordmark, welcome screens, and branding validation.
