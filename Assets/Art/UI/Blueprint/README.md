# Blueprint Workshop art guide

## Runtime assets

- `Backgrounds/whiteprint_grid.png`: light-mode architectural paper.
- `Backgrounds/blueprint_grid.png`: dark-mode blueprint paper.
- `Hatches/room-hatch-1.png` through `room-hatch-6.png`: repeatable concrete, wood, ceramic, steel, insulation, and survey patterns.
- `Rivet/rivet-sprite-sheet.png`: six transparent 512 x 512 mascot poses in a 3 x 2 sheet.
- `../../Logos/shikaku_blueprint_app_icon.png`: opaque app icon and splash art.
- `../Icons/icon_adventure_tower.png`: transparent single-color pixel-art skyscraper used by Adventure.
- `../Icons/icon_free_play_shikaku.png`: transparent filled-region mini Shikaku board used by Free Play.
- `Assets/Resources/UI/BlueprintThemeAssets.asset`: editor-facing runtime configuration.

The papers and hatches are deterministic assets produced by `Tools > Shikaku Go > Blueprint Workshop > Build Theme Assets`.

## Palette

- Blueprint navy: `#071D35`
- Whiteprint paper: `#ECF5F6`
- Cyan construction line: `#2290B9`
- Safety yellow: `#F4BC2B`
- Approval green: `#308B67`
- Review red: `#C43B45`

## Image generation provenance

Generation mode: built-in ImageGen, stylized-concept for Rivet and logo-brand for the icon.

### Rivet sprite sheet prompt

Use case: stylized-concept
Asset type: mobile puzzle game mascot sprite sheet
Primary request: Create a production-ready sprite sheet for Rivet, a playful construction helmet mascot for a calm blueprint-themed Shikaku puzzle app.
Scene/backdrop: genuinely transparent background
Subject: the exact same small yellow construction hard hat with expressive friendly ink-blue eyes in six equal square cells arranged as a precise 3-column by 2-row grid. Top row: welcoming with a cheerful expression; teaching with one brim corner acting like a pointer toward a tiny blueprint line; inspecting a tiny floor plan with a small magnifier. Bottom row: offering a rolled measuring tape as a hint; celebrating with a tiny green approved-plan stamp; concerned “not quite” expression while checking a small clipboard. The helmet itself is the full character, with optional tiny glove-like side tabs but no human head, body, or legs.
Style/medium: clean polished 2D mobile game UI character art, flat vector-like shapes with subtle matte plastic texture and restrained soft shading; readable at 80 pixels
Composition/framing: each pose centered and fully contained in its equal cell with generous transparent padding; consistent helmet proportions, face, palette, line weight, lighting, and scale across all six cells; no panel borders or dividers
Color palette: safety yellow helmet, deep navy-blue ink details, tiny cyan blueprint and approval-green accents
Constraints: genuinely transparent background and preserved alpha; no text, letters, numbers, logos, watermarks, extra characters, construction worker, face beneath the helmet, cropped edges, or shadows extending outside cells; exact 3 by 2 pose layout
Avoid: glossy 3D, photorealism, anime, baby proportions, busy construction props, saturated rainbow colors

### Blueprint app icon prompt

Use case: logo-brand
Asset type: final opaque mobile game app icon
Primary request: Create a polished square app icon for Shikaku Go with a blueprint architecture theme.
Scene/backdrop: completely opaque full-bleed deep navy blueprint paper with very subtle drafting grid texture
Subject: a bold simple white-and-cyan architectural floor plan divided into four unequal but perfectly rectangular rooms, with one small safety-yellow construction helmet marker integrated near the center. The rectangular room walls should make the Shikaku puzzle idea immediately readable.
Style/medium: crisp flat 2D game icon with restrained tactile paper grain and subtle line-weight variation; vector-friendly silhouette
Composition/framing: centered floor-plan mark filling the safe middle 72 percent of a square canvas; solid background extends to all four edges and corners
Color palette: deep blueprint navy, bone white, cyan construction lines, one safety-yellow helmet accent, tiny approval-green detail
Constraints: fully opaque full-bleed square image; no transparency or partially transparent corners, no text, letters, numbers, device mockup, outer icon frame, watermark, trademarks, buildings, skyline, tools, or extra objects; strong simple silhouette readable at 48 pixels
Avoid: rounded app-store mask, generic house icon, realistic construction scene, glossy 3D, photorealism, clutter, saturated rainbow colors
