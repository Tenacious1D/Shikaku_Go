The library arrangement below is superseded by the [upright bookcase revision](../UprightLibrary/README.md). Nursery refinements remain current.

# Bookshelves, libraries, and nursery alignment

The nursery keeps its existing traditional wooden style. Crib spindles now reach the lower rails, the end rails meet the frame, and curved rails use a continuous mesh strip. Rocking-chair feet connect to the runners; back spindles meet the curved crest; armrests share endpoints with their supports and seat edges.

The new FilledBookshelf asset has three packed rows of colorful books, a recessed wooden frame, and visible top/side faces. It is available in narrow rooms in either orientation. Large-room libraries use three to five separate shelves, with at least 12 cells and a short side of at least three cells. A room falls back to a reading arrangement if three shelves cannot fit. Every shelf avoids the clue cell and the other furniture. Dense boards keep the same primary shelf and omit additional shelves below 30 pixels per cell; furniture remains cell-scaled and obeys the existing editor toggle.

Catalog: 32 furniture kinds, 20 regular templates, 11 narrow templates. Adding template choices changes the deterministic selection for some existing room shapes.

## Current previews

- [Refined nursery, light](nursery-rocker-light.png) / [dark](nursery-rocker-dark.png)
- [Narrow bookshelf, light](bookshelf-light.png) / [dark](bookshelf-dark.png)
- [Library, light](library-light.png) / [dark](library-dark.png)
- [Larger library, light](library-large-light.png) / [dark](library-large-dark.png)

## Validation

- Actual game scripts compiled successfully using the installed Unity Roslyn compiler.
- 33 isolated Unity EditMode tests passed; report: [tests.xml](tests.xml).
- Exhaustive footprint checks cover room dimensions 1–8, every clue position, and four cell sizes. Tests also cover mesh bounds in both orientations, the 256-vertex per-prop budget, library eligibility/count/LOD, deterministic placement, and the furniture toggle.
- 36 renderer captures completed across nine cases, two themes, and 900×1100 / 540×900 sizes. Nursery, bookshelf, and library images were visually inspected.
- These are actual renderer fixtures, not screenshots of the full live game HUD. An on-device gameplay smoke test remains advisable.