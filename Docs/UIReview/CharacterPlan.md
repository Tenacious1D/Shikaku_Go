# Character pass

The app should feel like a friendly puzzle workshop: clean enough to focus, with Rivet, drawings, and construction giving progress a tangible reward.

## Restored now

- Adventure solved results use the existing building construction animation and floor-progress caption. Rivet stays out of this panel.
- Tutorials and other solved results show Rivet's existing expressions again.
- Existing Rivet placements in welcome and supporting dialogs are visible again.
- The modern spacing, readable controls, and light/dark palette remain.

## Implemented character treatment

1. **Give Rivet a clear role.** Make the tutorial feel guided by Rivet, with teaching, thinking, and celebrating expressions at the appropriate moments. Add a short arrival or celebration motion rather than continuous idle motion. Keep ordinary navigation focused.
2. **Make completion feel rewarding.** Preserve Adventure's floor-by-floor construction. Give other modes a brief Rivet celebration and a drawn approval mark. Highlight personal bests and streak milestones with their own concise treatment.
3. **Bring back crafted blueprint details.** Use small floor-plan illustrations, measured corner marks, and a distinctive approval stamp at selected focal points. Keep the background faint, avoid tiny decorative labels, and let the puzzle board remain the clearest grid.
4. **Give modes distinct identities.** Daily gets a calendar/stamp motif and warm amber; Time Trial gets a stopwatch and drawn timing marks; Free Play gets miniature plan collections and cool blue. Home introduces these motifs through expressive mode artwork.
5. **Warm up the visual voice.** Revisit the logo/title styling, introduce a little paper warmth to light surfaces, and use richer ink-blue surfaces in dark mode. Add friendly, brief contextual copy without replacing clear action labels.
6. **Coordinate motion and accessibility.** Define short button feedback, line-drawing transitions, and milestone celebrations. Add a static equivalent for reduced motion, preserve text contrast in both themes, and avoid movement during active puzzle solving.

## Review approach

Home, a tutorial step, and both result variants (Adventure building / other modes Rivet) were reviewed alongside Daily, Time Trial, Free Play, Settings, and Shop in light and dark.

Validate at compact phone, tall phone, and tablet sizes. Keep touch targets clear and verify the live tutorial positioning and solve-to-next transition on a device.

## Implementation notes

Implemented September 13, 2026.

- BlueprintCharacter draws the city elevation, plan sheets, stopwatch, calendar, drafting tools, and lightbulb as retained vector artwork. Existing Rivet sprite slices supply the contextual expressions.
- Home and the mode/page headers share these motifs. Daily has amber accents; Time Trial has distinct tier accents; Free Play adds plan sheets and small completion stamps. Settings and Shop receive restrained workshop details.
- The palette uses warm paper and richer ink-blue surfaces, with theme-specific illustration colors.
- Tutorial expressions follow the teaching step. Other solved panels animate Rivet briefly and draw an approval check. Personal bests use a gold treatment, based on the save service confirming a new record; the existing assisted/unassisted rules are preserved.
- Adventure solved panels retain the building construction and hide Rivet and the approval badge. Reduced motion resolves the building to its completed state immediately.
- New animations run only at tutorial-step/result events, finish in under half a second, and clear their scheduled work on detachment. The existing Reduce Motion preference disables the new movement and button scale feedback.

BlueprintCharacter.Install is idempotent and subscribes to settings changes only while its UI root is attached. Adventure map elements are never decorated by this component.

## Validation

- Unity application script compilation passed.
- 102 Unity previews passed layout checks across light/dark themes and three sizes.
- Runtime preview checks passed for animation movement and completion, tutorial expressions, reduced motion, mid-animation preference changes, detachment cleanup, idempotent installation, and Adventure result badge exclusion.
- On-device touch and platform service checks remain separate from these isolated rendering checks.
