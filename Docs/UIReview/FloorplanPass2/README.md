# Geometric room furniture — Pass 2

Direction 2 is implemented as procedural Unity UI geometry: solid room colors with shallow furniture faces and flat shadows. No generated bitmap assets, model imports, or new runtime packages are required.

## Editor and runtime control

Select the gameplay object's **BoardController** component. Under **Floorplan Furniture**, toggle **Show Room Furniture**. It defaults to on, persists when saved outside Play mode, and refreshes immediately when changed during Play mode.

A game-mode controller can call `board.SetRoomFurnitureEnabled(false)` to suppress furniture without changing floors, clues, input, progress, or saves. `RoomFurnitureEnabled` exposes the current setting. No mode-specific exclusions are enabled yet; Free Play and Time Trial use the same default until their policy is decided.

## Layout and scaling

- Beds, sofas, desks/chairs, cabinets, armchairs, tables, and plants use a small collection of flat shapes in one pooled mesh per room.
- Furniture only appears in valid committed regions. It does not inspect the solution, decorate drag previews, or furnish invalid placements.
- Geometry is measured in grid-cell units, so it shrinks naturally as cells get smaller. Every footprint includes its side faces and shadow.
- The clue's entire cell is reserved, regardless of its position. Room wall insets and spacing prevent overlapping objects.
- Stable room geometry and clue coordinates select a layout. Removing and redrawing the same room, loading it again, or switching theme does not reroll its furniture.
- Below 30 screen pixels per cell, accessories disappear. Below 12 pixels per cell, or when a main silhouette cannot fit legibly, furniture is omitted. There is no fixed minimum pixel size that could push a prop across walls.
- Narrow rooms use compact cabinets; single-cell rooms stay clear. Light and dark themes use coordinated furniture colors.
- Furniture renders above counting lines and below walls, hints, and drag feedback. It shares the room fade and respects reduced motion. Every furniture mesh is noninteractive.

## Validation

- The actual game scripts compile with Unity's compiler (existing unrelated warnings remain).
- 25 tests pass, including exhaustive clue clearance and bounds checks for every clue position in room dimensions 1–8, with four cell sizes; deterministic recommit; small-cell detail reduction; invalid/single-cell behavior; and the noninteractive toggle.
- Unity renderer captures cover 12 board states × two themes × two resolutions (900×1100 and 540×900). Cases include the approved concept layout, a 14×14 board with small rooms, a 13×12 board, masked floors, narrow regions, selection, hint, drag, furniture off, and pooled reuse after restart/toggle.
- These captures use the real room/furniture/wall renderer and puzzle model with fixture cell grids and labels. They are board previews, not full device gameplay screenshots. Device touch and complete HUD integration still need a smoke test in the running game.

## Previews

- [Light furniture](concept-light.png) / [Dark furniture](concept-dark.png)
- [Furniture off](off-light.png)
- [Dense small rooms at mobile size](small-rooms-light.png) / [Dark](small-rooms-dark.png)
- [Masked floor](masked-light.png) / [Narrow rooms](narrow-light.png)