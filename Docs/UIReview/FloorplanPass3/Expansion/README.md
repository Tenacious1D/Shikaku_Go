The nursery artwork below is superseded by the [traditional nursery redraw](../Traditional/README.md).

# Pass 3 — approved furniture expansion

Implemented September 22, 2026. This extends Pass 3's catalog in the existing geometric style.

## Added furniture

- Crib, changing table, and rocking chair for two nursery variants.
- Grandfather clock with clock face and optional pendulum detail.
- TV on a low media console, also paired with a sofa in entertainment rooms.
- Double-door wardrobe, as a primary storage piece and a bedroom accessory.
- Toy chest, paired with nurseries and a bedroom variant.
- Breakfast bar with two stools, available in kitchen layouts and narrow rooms.

The catalog now has 23 furniture kinds, 16 regular room templates, and 7 narrow-room templates. Supporting objects remain related to each room's use; layouts contain at most three objects.

## Nursery and small-screen rules

Nurseries require at least 12 cells, a short side of at least 3 cells, a crib that fits at 85% of its nominal cell-relative size or larger, and geometric space for at least one supporting piece. Cramped candidates fall back to a reading-room layout. This eligibility check is independent of screen resolution, so reducing detail does not change the room family.

All props retain the existing cell-relative scaling, clue-cell clearance, wall inset, deterministic placement, light/dark palette, pooling, and noninteractive rendering. Accessories drop out on small cells; crib slats, pendulums, chest markings, and other fine details simplify. Clock hands and TV screens remain readable identifiers.

**BoardController → Floorplan Furniture → Show Room Furniture** still controls all furniture. `SetRoomFurnitureEnabled(bool)` remains available for future mode-specific policies. Adventure art, puzzle rules, mascots, and solved panels are unchanged.

## Previews

- [Expanded catalog — light](approved-batch-light.png) / [dark](approved-batch-dark.png)
- [Nursery with changing table](nursery-light.png) / [dark](nursery-dark.png)
- [Nursery with rocker](nursery-rocker-light.png) / [dark](nursery-rocker-dark.png)
- [Narrow clock, TV, and breakfast bar](slender-additions-light.png) / [dark](slender-additions-dark.png)
- [Dense board](small-rooms-light.png) / [dark](small-rooms-dark.png)
- [Furniture off](off-light.png) / [dark](off-dark.png)

Read the expanded catalog left to right: changing-table nursery, rocker nursery, entertainment room; wardrobe, bedroom variant, breakfast bar; grandfather clock, existing kitchen, existing reading room.

## Validation

- Actual game scripts compile with Unity's compiler; existing unrelated warnings remain.
- 31 tests passed. The suite checks every clue position in rooms up to 8×8, all furniture bounds in both orientations and detail levels, nursery eligibility/supporting space, stable layouts across detail levels, narrow clock/TV/bar coverage, invalid regions, and the existing toggle behavior.
- Every furniture piece stays within its collision footprint, including side faces and shadows, with a tested ceiling of 256 vertices per piece.
- 72 Unity renderer captures cover 18 states, two themes, and two resolutions. Light catalog and dark narrow-room captures were visually reviewed.
- Captures use the real room/furniture/wall renderer and puzzle model with fixture cell labels/grids. They are board previews rather than full device gameplay captures; device gameplay still needs a smoke test.