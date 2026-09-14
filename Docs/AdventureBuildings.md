# Adventure city and buildings

The Adventure map and solved panel share BuildingView. Chapter content and the
existing immutable puzzle completion IDs are the only sources of floor geometry
and progress; building appearance is content configuration, not save data.

## Chapter appearance

Editable definitions for the five shipped chapters live in
Assets/Resources/Buildings/Chapter01Building.asset through Chapter05Building.asset.
Select one in the Unity Inspector:

- Leave Custom Style empty for automatic appearance. The stable building ID (or
  packId, falling back to the pack path) and appearance seed select one of eight
  coordinated palettes and four architectural treatments. It never rerolls on
  launch, progress changes, or map/solved transitions, and does not consume
  UnityEngine.Random state.
- Reroll Automatic Appearance increments the seed deliberately.
- Enable Override Architecture to select Classic, Modern, Warehouse, or ArtDeco.
  These change window proportions, mullions, facade trim and roof-edge treatment.
- Enable Override Colors to choose wall and roof colors. Coordinated side,
  terrace, edge and window colors are derived from those choices.
- Assign a Building Style asset for reusable custom textures and palette values.
  Overrides apply to a runtime copy, leaving the shared style asset unchanged.
- Map Offset adjusts the plot in design units, clamped to a small local adjustment
  to preserve room for nearby streets and buildings.

To add a chapter, add a valid Story JSON pack using the existing content convention.
Its floor sequence, automatic appearance and city plot are generated automatically.
A definition is optional. For custom configuration, use Create > Shikaku >
Adventure > Building Definition, save under a Resources/Buildings folder and set
chapterPackPath, e.g. Story/Adventure_Chapter06 (no Puzzles/ prefix or .json).
Keep buildingId/packId stable when moving content so automatic colors stay stable.
Definitions and chapter geometry are cached per Play Mode session.

## Exact floor shapes and masks

PuzzleCatalog's puzzles[] order is the bottom-to-top floor order.
Each entry's width and height independently override the pack defaults, exactly
as in PuzzleLoader. Entry masks override pack masks; missing masks mean a complete
rectangle. The existing PuzzleModel.BuildExistsFromMask parser supplies occupied
cells, after validation rejects malformed or empty masks.

Columns become world width and rows become world depth. The grid is centered
using its original dimensions, without recentering the remaining cells of an
asymmetric mask. Each floor may have its own dimensions AND mask.

Chapter 5 is a 7x7 grid with six perimeter cells removed: 43 occupied cells.
Its foundation, constructed floors and finished roof all use those same 43 cells.
Courtyards and perimeter notches remain empty. Visible walls are generated only
at occupied-to-empty boundaries, including recessed walls.

The cached geometry splits cells into half-unit patches so odd/even grid
dimensions line up on a common coordinate lattice. Patches are ordered by
ground-plane depth, then elevation within each column. This handles concave
buildings and changing floor shapes without relying on a whole-floor painter
order. Covered terrace patches are skipped. Roof caps and rims follow the final
floor's occupancy, and decoration is restricted to an occupied cell.

BuildingView draws batched quads inside one retained VisualElement. No GameObjects
or VisualElements are created per cell, window or floor. The precomputed patch
order is reused. There are no runtime Update loops for buildings or streets;
only an active solved-floor animation schedules redraws.

## City map

AdventureCityMap groups five chapters into a compact neighborhood: two shared
courtyards, a third building beside a public garden, and a diagonal main street
with one branch. Ground paving, cars and garden pieces use BuildingGeometry's
30-degree isometric projection. Streets separate groups rather than each chapter.
Larger catalogs add mirrored neighborhoods above the existing ones; plot anchors
stay fixed as construction progresses. Layout scales with the available width.
Small author offsets are clamped to +/-2 horizontal and +/-4 vertical design units
to keep neighboring building hit areas separate.

Each chapter keeps the controller's original Button and navigation callbacks.
The button has a transparent hit area covering its building and a small address
plaque beneath it. Completed plaques show a check and chapter number; unfinished
plaques show the chapter number and floor progress or Locked. Tooltips retain
the full chapter/progress description, and focus/pressed states highlight the
plaque. Picking ignores scenery and the building mesh so taps reach the button.

AdventureCityScenery draws all streets and decorative props in one retained
VisualElement behind the buildings. Reusable vector modules include tree canopies,
parked cars, benches, planters, streetlights and a garden fountain. Planting sites
are curated around sidewalks and the park, with small stable seeded variations
per neighborhood. Nothing rerolls on theme changes, progress updates or revisits.
No additional GameObjects, saved state, animation framework or idle Update loops
are required. Public courtyards are scenery; each building's actual foundation
and floor masks still come exclusively from the shared BuildingView.

ThemeManager's existing light/dark ancestor classes select the map and plaque
palettes in AdventureScreen.uss. The --city-sidewalk, --city-asphalt,
--city-marking, --city-paving, --city-lawn, --city-leaf and --city-night custom
properties also repaint the scenery. Daytime uses warm paving and green trees;
nighttime uses cool courts, darker greenery and small warm streetlight halos.
Chapter building colors and the solved panel renderer stay consistent.

## Solved panel and saves

GameplayHUD still captures completion before MarkCompleted and animates the
specific newly solved puzzle's floor. The same resolved palette, occupied-cell
geometry and BuildingView code are used on both screens. The 220 ms pre-state
pause and 650 ms fade/settle remain. Replays are idempotent. Closing the modal
finishes presentation without altering saves or blocking navigation.

FloorConstructed and ChapterCompleted events are available for optional sound or
effects. The final roof appears after the final construction completes.

## Final artwork

BuildingStyle.palette supports foundationTile, wallModule, windowModule,
edgeModule, terraceTile, roofTile and roofDecoration textures. Null textures use
colored modular geometry. Artwork is ordinary front-facing unit art, projected
by the renderer; half-unit fragments preserve UV portions of the original module.
Set palette colors to white where untinted textures are desired.

## Preview and validation

Shikaku > Adventure > Building Preview offers the actual shipped chapters plus a
synthetic chapter with notches, a courtyard, setbacks and an overhang. Compare two
view sizes, choose a definition override or appearance seed, inspect any floor
count and animate construction. It does not touch player completion.

Focused tests cover shipped dimensions and masks, Chapter 5's 43-cell foundation,
recessed boundaries, mask inheritance/overrides, invalid masks, odd/even alignment,
stable style selection, asset isolation, replays, construction completion and
detachment. Rendering tests capture the shared renderer and the real Adventure
screen with its UXML/theme sheets in both themes, verify live street recoloring,
check compact density, building hit targets and chapter navigation, and check a
40-chapter layout for overlaps and narrow viewport bounds.

Run EditMode tests matching Shikaku.Tests.AdventureBuilding with graphics enabled.
Captures and test reports go to Logs/. Tests use a temporary UI host and isolated
save file. They do not need Play Mode or the project's online services. During
development with the main project already open, validation can run in an isolated
copy to avoid interrupting the active editor.

On-device touch target, safe-area and GPU performance checks are still recommended
before release.

## Resetting progress in the Unity editor

Exit Play Mode, then choose Shikaku Go > Progress > Reset Puzzle Progress... .
The confirmation explains the scope: all puzzle completion and best times,
Free Play/Adventure unlocks and continue positions, Daily completion and streak,
and Time Trial scores. Hints, purchase/reward records, achievements, tutorial
completion and device preferences are preserved. Enter Play Mode again to see
unbuilt chapter foundations and the first chapter unlocked.

The command updates SaveManager's cached data, shikaku_save.json and its .bak
recovery file. It also creates a separate timestamped before-progress-reset JSON
snapshot beside the save; the Console prints its path. This snapshot is for manual
recovery and is never loaded automatically. Clearing PlayerPrefs alone does not
reset puzzle completion. The reset command is available only in the editor and
is disabled during Play Mode or script compilation.