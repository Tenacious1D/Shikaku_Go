Current nursery alignment refinements are shown in [Library](../Library/README.md). The images below preserve the earlier traditional nursery iteration.

# Traditional nursery and additional room variations

Implemented September 22, 2026. These nursery drawings replace the earlier crib and rocking-chair artwork.

## Nursery redraw

- The crib is an open wooden frame with rounded posts, ball finials, arched end rails, and visible spindles on all sides. Shallow projected faces expose the mattress and rails instead of drawing a solid rectangular bed frame.
- The rocking chair has its own geometry: wooden seat and arms, a tall spindle back, curved crest, four supporting legs, and two bowed runners. It no longer reuses the padded armchair drawing.
- The crib footprint is wider; the rocker is larger so its silhouette reads clearly. Both remain inside their reserved furniture bounds, including curves, feet, and shadows.
- Nursery eligibility remains at least 12 cells and a short side of 3 cells, with space for the crib and supporting furniture. Low-detail rendering reduces spindle counts while retaining the characteristic open frame and curved runners.

## New room variations

| Room | Furniture |
| --- | --- |
| Home office | Double workstation with two chairs, filing cabinet, printer stand |
| Music room | Upright piano and aligned bench, record cabinet with turntable, plant |
| Storage | Shelves with boxes, coat rack, woven laundry hamper |

The piano and its bench use one placement footprint, so their orientation and spacing stay together. Home offices require at least 10 cells; music rooms require at least 6. Both require a short side of at least 2 cells. Shelves, coat racks, and hampers also extend the narrow-room catalog.

There are now 31 furniture kinds, 19 regular templates, and 10 narrow templates. Existing clue clearance, cell-relative scaling, light/dark colors, furniture toggle, pooling, and noninteractive behavior remain in place.

## Current previews

- [Traditional nursery with rocker — light](nursery-rocker-light.png) / [dark](nursery-rocker-dark.png)
- [Traditional crib with changing table — light](nursery-light.png) / [dark](nursery-dark.png)
- [Home office — light](home-office-light.png) / [dark](home-office-dark.png)
- [Music room — light](music-room-light.png) / [dark](music-room-dark.png)
- [Storage — light](storage-room-light.png) / [dark](storage-room-dark.png)
- [Dense mobile board](small-rooms-light.png)

## Validation

- Actual game scripts compile with Unity's compiler; unrelated existing warnings remain.
- 32 tests pass, including all furniture kinds, every clue location in rooms up to 8×8, both drawing orientations/detail levels, nursery and office/music size requirements, narrow-room coverage, toggles, and deterministic placement.
- Every prop remains within its collision footprint and the existing 256-vertex budget, including the curved nursery meshes.
- 84 Unity board captures cover 21 states × two themes × two resolutions. Dedicated nursery, home office, music, and storage renders were visually reviewed.
- Previews use the real room/furniture/wall renderer and puzzle model with fixture cell grids and labels; a full device gameplay smoke test remains separate.