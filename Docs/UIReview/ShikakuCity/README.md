# Shikaku City branding polish

The home and welcome screens now use a native vector city logo made from colorful rectangular puzzle-room silhouettes. The mark has theme-aware outlines and windows and scales without a raster texture. The main menu has a new Shikaku City wordmark, refined spacing, quieter card edges, and the tagline “Little puzzles. A city of possibilities.”

The display name is updated in Unity Player Settings, welcome/privacy text, rating prompts, support report text, notification titles, and editor release checks. Mobile bundle identifiers, purchase identifiers, and cloud project identity are unchanged. Desktop JSON progress and its backup retain the previous Shikaku Go directory; desktop PlayerPrefs are not migrated between product names.

Adventure map geometry, its building-only solved animation, and Rivet in tutorials and other solved panels remain intact. Time Trial continues to exclude furniture in its menu and puzzles.

## Previews

| View | Light | Dark |
|---|---|---|
| Main menu | [Preview](safe-area-light-0.png) | [Preview](safe-area-dark-0.png) |
| Compact main menu | [Preview](safe-area-light-1.png) | [Preview](safe-area-dark-1.png) |
| Compact welcome | [Preview](privacy-welcome-screen-light-1.png) | [Preview](privacy-welcome-screen-dark-1.png) |
| Compact rating prompt | [Preview](rate-prompt-light-1.png) | [Preview](rate-prompt-dark-1.png) |

## Validation

- Actual app runtime scripts compiled against the installed Unity assemblies.
- 72 isolated Unity captures covered 12 screen states in light/dark themes at 1080×2340, 960×1700, and 1600×2100. The harness completed its layout, repeat-initialization, mascot, and Adventure-animation assertions.
- 47 isolated EditMode tests passed: the existing 42 puzzle/floorplan checks, four save persistence/recovery checks, and a desktop save-directory compatibility check. The full-catalog puzzle-reset test was excluded from this isolated fixture because its catalog dependencies are not installed there. See [test results](tests.xml).
- Main menu, compact welcome, and rating prompt captures were visually inspected.

These previews use fixture data. They do not validate physical-device safe areas, touch input, platform notifications, purchases, or store metadata. The operating-system launcher icon has not been replaced by this menu-logo change.