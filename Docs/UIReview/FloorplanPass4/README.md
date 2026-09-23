# Pass 4 — doors, windows, and room reveal

## Architectural details

Valid neighboring player-created rooms can receive one doorway per shared partition. Swinging doors appear when the complete swing clears the clue cell and furniture. Tight rooms use a compact sliding-door leaf inside the wall margin. The renderer reserves all furniture, including accessories currently hidden at lower detail or by the furniture toggle, so turning furniture on cannot create a door collision. If neither style fits safely, the partition remains solid.

Windows use a restrained blue glass strip with a frame and central mullion. They appear only on exterior walls of valid rooms, at most once per room per exterior side, and skip clue-adjacent segments. Masked courtyard boundaries and unfinished/invalid rooms do not receive openings.

Door thresholds retain a thin line at the exact puzzle boundary. Selection, hints, and invalid-state wall coloring still apply. Openings are decorative: they do not change region validity, input, scoring, or require the floorplan to form a connected walking route. They are inferred only from committed player geometry and public clues; solution data is never used.

At less than 36 screen pixels per cell, all openings simplify to solid walls. Swing arcs appear at 44 pixels per cell and above. Light and dark themes share identical placement. Deleting/recommitting rooms and restarting rebuild the details without leaving stale openings.

## Room reveal

The existing floor fade is followed by a 240 ms furniture reveal after a 60 ms delay. Each prop settles from 94% to 100% inside its own reserved footprint, rather than moving the entire room's furniture toward a clue. Reduced-motion mode displays the finished room immediately. Pooled views reset their reveal when reused, and theme/selection refreshes do not restart it. There is no idle animation.

The earlier furniture catalog, upright library rows, and furniture toggle are retained. Adventure map art, building celebrations, tutorial/solved mascots, and menus were outside this pass. Menu rollout remains Pass 5.

## Previews

- [Completed floorplan — light](architecture-light.png) / [dark](architecture-dark.png)
- [Larger rooms and swinging doors — light](door-study-light.png) / [dark](door-study-dark.png)
- [Masked board — dark](masked-dark.png)
- [Dense mobile puzzle](small-rooms-light.png)
- [Reveal midpoint](reveal-mid-light.png) / [finished](reveal-end-light.png)
- [Upright library](library-light.png)

## Validation

- Actual game scripts compiled successfully with the installed Unity compiler.
- 42 Unity EditMode tests passed: [test report](tests.xml).
- Tests cover door/clue/furniture clearance across room shapes and clue positions, window eligibility, invalid and masked regions, creation-order stability, removal/restart, all swing orientations, small-scale simplification, and bounded/reduced-motion reveal progress, plus the existing furniture and floorplan regressions.
- 60 captures completed across 15 fixtures, two themes, and two resolutions (900×1100 and 540×900). Completed, larger-room, masked-dark, and dense-mobile layouts were visually inspected. Reveal captures sample the actual reveal function at 180 ms and 300 ms.
- Previews use the actual room, furniture, and wall renderer in an isolated Unity fixture. Full on-device gameplay and animation timing still need a smoke test.

## Pass 5 completed

The menu rollout is documented in [Pass 5](../FloorplanPass5/README.md).
