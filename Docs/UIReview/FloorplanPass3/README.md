# Room template coverage — Pass 3

Pass 3 expands the geometric furniture renderer into nine related room families:

| Family | Main object | Supporting objects |
| --- | --- | --- |
| Bedroom | Bed | Bedside cabinet, plant |
| Lounge | Sofa | Coffee table, plant |
| Office | Desk and chair | Bookcase, plant |
| Dining | Table with four chairs | Sideboard, plant |
| Kitchen | Counter with sink and cooktop | Cabinet, plant |
| Bathroom | Tub | Basin, toilet |
| Reading room | Armchair | Bookcase, side table |
| Laundry | Washer | Basin, cabinet |
| Storage | Bookcase | Cabinet, plant |

Narrow rooms select cabinets, bookshelves, or benches. Main objects and accessories can turn by 90 degrees to fit the available space. Room size limits the eligible families: beds, dining sets, and bathrooms require at least six cells. Larger rooms can use a modestly larger main object, while every object still scales with the actual grid cells.

The layout reserves the entire clue cell at any location, includes shadows in collision bounds, and uses at most three objects per room. Selection remains deterministic from room geometry and clue location, independent of theme and creation order. Screens below 30 pixels per cell retain one main silhouette; under 12 pixels per cell furniture disappears. Small new details (plates, cooktop rings, taps) drop out below 36 pixels per cell. Accessories also require a readable physical footprint.

The renderer uses flat geometric faces, supports light/dark themes, and draws above the counting grid and below feedback. All furniture remains noninteractive and uses the existing pool, room fade, and reduced-motion handling.

## Toggle and future mode control

The existing **BoardController → Floorplan Furniture → Show Room Furniture** Inspector toggle still controls every template. Mode controllers can use `SetRoomFurnitureEnabled(bool)`; Time Trial now always hides furniture, while other modes respect the toggle.

## Scope

Doors, windows, additional animation, and menu rollout remain in later passes. Adventure map art and solved-panel/mascot behavior are unchanged.

## Validation

- The actual game scripts compile with Unity's compiler; unrelated pre-existing warnings remain.
- 29 tests pass. They cover all clue locations for rooms up to 8×8 at four cell sizes, stable recommits, small-screen simplification, invalid rooms, toggling, room-family coverage, narrow-room orientation, and every furniture mesh's bounds in both orientations at both detail levels.
- Each prop is checked against a maximum of 256 vertices. A room has at most three props and one furniture graphic.
- Board renderer previews exercise 14 states in light/dark themes at 900×1100 and 540×900: 56 captures. These include the nine-family catalog, central clues, dense and masked boards, narrow rooms, selection, hint, draft, off, and pooled reuse after restart.
- Previews use the real room/furniture/wall renderer and puzzle model with fixture cell grids and labels. They do not replace a full device gameplay smoke test.

## Previews

- [Nine-room catalog — light](catalog-light.png) / [dark](catalog-dark.png). Read left-to-right: bedroom, lounge, office; dining, kitchen, bathroom; reading, laundry, storage.
- [Central clues — light](central-clues-light.png) / [dark](central-clues-dark.png)
- [Dense mobile board](small-rooms-light.png)
- [Narrow rooms](narrow-light.png)

## September 22 catalog expansion

The approved nursery, clock, TV console, wardrobe, toy chest, and breakfast-bar batch is documented with updated previews in [Expansion](Expansion/README.md).


## Current nursery and room variations

The nursery has been redrawn with a traditional wooden crib and rocking chair. The new home office, music room, and storage variations are documented with current previews in [Traditional nursery and room variations](Traditional/README.md).


## Library and nursery alignment update

See [the current nursery refinements, filled bookshelf, and library previews](Library/README.md) for the latest meshes and validation.


## Upright library revision

The current library uses a bank of tall bookcases standing side by side. See [current previews and validation](UprightLibrary/README.md).


## Pass 4 implemented

Doors, exterior windows, and a reduced-motion-aware furniture reveal are documented in [Pass 4](../FloorplanPass4/README.md).
