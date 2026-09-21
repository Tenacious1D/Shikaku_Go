# Floorplan foundation — pass 1

Implemented September 20, 2026.

## Changes

- One shared wall mesh replaces four independent walls per region. Each physical cell edge is emitted once, including T-junctions and boundaries between equal-area rooms.
- The heavier perimeter follows playable cells and interior cutouts. The old rectangular tray outline is disabled.
- Room floors meet without cell gutters. Six coordinated light/dark floor colors replace the general puzzle palette and room hatches.
- Unplaced cells retain a clear counting grid; placed rooms use a much fainter internal grid.
- Selection and hint accents remain amber. Invalid rooms retain red boundaries and error fill. Drag feedback and tutorial highlights remain separate overlays.
- Room fill animation no longer scales the room away from its walls. Turning on reduced motion resolves the floor fade immediately.

Furniture, doors, windows, and further menu restyling are outside this pass.

## Implementation

FloorplanEdges builds topology only from current player placements and the playable mask; it never reads the solution. FloorplanWallGraphic draws the noninteractive mesh and resolves selection, invalid-room, and hint colors. BlueprintRoomDecorationLayer retains pooled floor fills and existing preview/solve feedback.

The theme asset explicitly stores the new surface, line, and room colors, making the changes available to existing editor sessions.

## Verification

- Actual game scripts compile with Unity's compiler.
- 20 tests passed: shared-wall regression cases and the existing puzzle-model/decoration tests, run in an isolated Unity project. See tests.xml.
- 36 rendered cases cover nine states, two themes, and two image sizes: 900 × 1100 and 540 × 900.
- Board sizes include 4 × 4, 8 × 6, 13 × 12, a masked 8 × 8, and narrow rooms.
- The renders use the actual room/wall renderer and puzzle model with fixture cell objects and counting-grid labels. They do not represent a full device gameplay session; live touch, full HUD layout, and hint-pulse timing still need device smoke testing.
- Existing working changes outside the floorplan foundation were preserved.

## Previews

| Board state | Light | Dark |
|---|---|---|
| Empty floor | [Preview](empty-light.png) | [Preview](empty-dark.png) |
| Placed rooms | [Preview](placed-light.png) | [Preview](placed-dark.png) |
| Mixed / invalid | [Preview](mixed-light.png) | [Preview](mixed-dark.png) |
| Dense floor | [Preview](dense-light.png) | [Preview](dense-dark.png) |
| Masked floor | [Preview](masked-light.png) | [Preview](masked-dark.png) |
| Narrow rooms | [Preview](narrow-light.png) | [Preview](narrow-dark.png) |
| Selected room | [Preview](selected-light.png) | [Preview](selected-dark.png) |
| Hint accent | [Preview](hint-light.png) | [Preview](hint-dark.png) |
| Drag preview | [Preview](draft-light.png) | [Preview](draft-dark.png) |
