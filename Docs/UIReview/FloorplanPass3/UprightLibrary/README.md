# Upright library revision

This replaces the earlier scattered library arrangement. TallBookshelf is a distinct, upright five-tier wooden cabinet with a narrow silhouette, book-filled shelves, a cornice, a base, and a shallow visible side. The existing low FilledBookshelf remains available for narrow rooms.

Libraries now fit a single bank of three to five tall bookcases as one group. All cases have identical dimensions, a shared baseline, close equal gaps, and the same upright facing. Placement moves the entire bank around the clue cell; individual cases never rotate or scatter. If a complete bank cannot fit, the room uses a reading-room fallback. Dense boards keep the bank together while simplifying book detail. Existing cell-relative scaling, dark mode, and furniture controls still apply.

- [Library — light](library-light.png) / [dark](library-dark.png)
- [Larger library — light](library-large-light.png) / [dark](library-large-dark.png)

Validation: game scripts compile successfully; 33 Unity EditMode tests pass, including exhaustive footprint/clue checks, upright orientation, equal dimensions, adjacency, row alignment, stable low-detail layout, and the per-prop mesh budget. Report: [tests.xml](tests.xml). All 36 renderer captures completed; the mobile light library and dark larger library were visually inspected. These previews use the actual room renderer in an isolated fixture, not the full live game HUD.