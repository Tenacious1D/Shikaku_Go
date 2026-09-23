# Pass 5 — floorplan menu rollout

Home, Daily, Time Trial, Free Play, Settings, Shop, and privacy screens now share the furnished puzzle's warm paper, sage, blue, peach, and muted dark-room colors. The menu-wide blueprint grid is removed. Architectural character comes from furnished plan illustrations, restrained card outlines, room-colored mode cards, and the existing character sketches.

## Changes

- Home uses distinct room colors for each mode, an illustrated Free Play entry, and the tagline “Little rooms. Big ideas.”
- Time Trial cards have unrotated, unfurnished plans with separate, readable grid-size badges. Time limits and best scores retain their current bindings.
- Free Play collection and size cards receive native vector plan illustrations, including dynamically created cards. Completed and next levels use sage and warm-gold states while keeping their status labels.
- Daily retains its calendar structure, intrinsic row sizing, scrollable body, and difficulty states; dates and difficulty controls use the room palette and simpler corners.
- Settings, Shop, and privacy retain their existing controls and flows with consistent surfaces, focus colors, separators, and button shapes.
- Decorative illustrations ignore pointer input, scale to their available space, update their colors with the theme, and do not animate. They are illustrative mini floorplans, not previews of puzzle solutions.

Adventure's map and chapter UI remain outside the opt-in menu style. The Adventure building celebration, Rivet tutorial/non-Adventure result appearances, puzzle furniture, door/window rendering, and gameplay behavior are preserved. No purchases, saves, analytics preferences, or platform services were changed by this pass.

## Previews

| Screen | Light | Dark |
| --- | --- | --- |
| Home | [Preview](safe-area-light.png) | [Preview](safe-area-dark.png) |
| Daily | [Preview](daily-screen-light.png) | [Preview](daily-screen-dark.png) |
| Six-row calendar | [Preview](daily-six-row-light.png) | [Preview](daily-six-row-dark.png) |
| Daily difficulty | [Preview](daily-modal-light.png) | [Preview](daily-modal-dark.png) |
| Time Trial | [Preview](time-trial-screen-light.png) | [Preview](time-trial-screen-dark.png) |
| Free Play collections | [Preview](free-play-screen-light.png) | [Preview](free-play-screen-dark.png) |
| Grid sizes | [Preview](free-play-size-light.png) | [Preview](free-play-size-dark.png) |
| Levels | [Preview](free-play-level-light.png) | [Preview](free-play-level-dark.png) |
| Settings | [Preview](settings-screen-light.png) | [Preview](settings-screen-dark.png) |
| Shop | [Preview](shop-screen-light.png) | [Preview](shop-screen-dark.png) |
| Welcome | [Preview](privacy-welcome-screen-light.png) | [Preview](privacy-welcome-screen-dark.png) |
| Privacy | [Preview](privacy-settings-light.png) | [Preview](privacy-settings-dark.png) |

## Validation

- Actual application scripts compiled successfully using the installed Unity compiler.
- 108 Unity renders completed: 18 screen/modal states × two themes × three sizes (1080×2340, 960×1700, 1600×2100).
- Runtime render assertions checked calendar containment, card horizontal bounds, opt-in coverage, duplicate initialization, noninteractive illustrations, and Adventure exclusion.
- Existing result/tutorial checks verified visible mascot sprites, a completed building construction animation, viewport containment, and the building-only Adventure result. Reduced-motion result/tutorial states were included.
- Home, Time Trial, a short-screen six-row calendar, dark grid-size cards, Settings, and Shop were visually inspected. No stylesheet import errors were reported. `git diff --check` passed.

The previews use actual UXML/USS and vector rendering with representative menu data. They do not exercise live billing, restored purchases, device touch, or physical safe-area behavior; those still need a device smoke test.

## Time Trial furniture policy

Furniture is now always hidden in Time Trial gameplay, including subsequent puzzles in a run. Other modes retain the existing editor preference. Time Trial menu art also omits furniture while retaining floor colors, walls, and size labels. The Time Trial previews above have been refreshed. Application compilation passed, and 12 focused menu renders (Time Trial and Free Play, two themes, three sizes) completed successfully.
