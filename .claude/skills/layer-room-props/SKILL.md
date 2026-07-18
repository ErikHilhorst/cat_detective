---
name: layer-room-props
description: Turn a flat pre-rendered room background into walk-behind foreground props (cat Y-sorts behind them, they fade when the cat is behind). Uses tools/layer_room_props.py with the Gemini image API - magenta-silhouette masking + inpaint removal - then wires room_config props[], a Tiled Triggers layer, and Content.mgcb. Replaces the manual Photoshop cut-out workflow.
---

# Layer room props (walk-behind foreground)

Automates the manual Photoshop workflow: for chosen objects in a room's `bg_base`, produce
(a) a new `bg_base` with those objects removed (inpainted floor/wall) and (b) per-prop
full-canvas RGBA overlays cut from the ORIGINAL art, then wire them as `Prop` entities
(Y-sorted, fade to 0.4 alpha when the cat's feet enter their trigger zone). Also enables
hiding interactables behind props: an interactable whose sprite Y-sorts behind a prop is
revealed when the prop fades (see the entrance guest registry behind `prop_console`).

## Why the pipeline looks the way it does (do not "simplify" it back)

The Gemini image model re-renders the WHOLE image on every edit (subtle global repaint +
a few px of drift), so a naive before/after diff cannot isolate the edited object, and
asking it to generate transparent layers directly can never align pixel-perfect. The tool
therefore makes two independent edits per prop, both against the original scene, with a
red marker rectangle drawn on the input (text descriptions alone pick the wrong object in
prop-dense scenes):

1. **MASK call** - recolor the marked object to a flat magenta silhouette. "Where is
   magenta" is immune to re-render noise (detected by hue: the model often paints a darker
   magenta than requested). Locally aligned back to the original via translation search;
   the overlay is cut from the ORIGINAL pixels - zero AI pixels in the prop.
2. **REMOVAL call** - remove the marked object, inpaint behind it. Those pixels land in
   the new bg only INSIDE the dilated mask (soft edge); everywhere else keeps original
   pixels. Fill misalignment is invisible: that area sits behind the prop and is only seen
   at 40% alpha during a fade.

Both calls are validated programmatically (magenta concentrated in the declared region /
in-mask change high enough / no marker-red leaked into the fill) and retried under a
per-prop cap and a global image budget. All attempts are saved to
`debug_output/layering/<room>/` and reused on reruns before any new API call.

## Prerequisites

- `GEMINI_API_KEY` in `.env` at the repo root or the environment.
- Python with Pillow + numpy + requests.

## Workflow

### 0. Optional: declutter previews first

`tools/declutter_rooms.py --manifest tools/declutter_manifest_malibu.json` generates
review-only "flat floor" variants of whole rooms (all free-standing objects removed) under
`debug_output/layering/<room>/declutter_v*.png` - useful for deciding which objects should
become props before committing. `--refine 1` chains a second pass on v1 to remove
leftovers. CAVEATS: the model removes only part of a long list per pass, and chained
passes start hallucinating (moving objects, inventing new furniture/rugs) - treat outputs
as Photoshop raw material, never wire them in unreviewed. These images touch nothing under
`Content/`.

### 1. Pick props and write the manifest

Look at the room's `bg_base` (Read the image) and the map's `Collisions` layer. Good
props stand ON WALKABLE FLOOR with the cat able to get behind them (feet Y above the
prop's floor contact). Avoid objects overlapping transfer zones so heavily that the cat
transfers before the fade is visible. 2-4 props per room is plenty.

Manifest at `tools/layer_manifest_<case>_<room>.json`:

```json
{
  "case": "malibu_mansion",
  "room": "entrance",
  "background": "bg_base.jpg",
  "model": "gemini-2.5-flash-image",
  "max_total_images": 12,
  "max_attempts_per_prop": 3,
  "props": [
    { "id": "prop_stair_plant",
      "remove": "the small potted green plant standing on the marble floor ...",
      "region": [650, 430, 120, 125] }
  ]
}
```

- `id` must start with `prop_` (becomes the PNG name and content path).
- `region` is `[x, y, w, h]` in bg pixels, generous but tight enough to disambiguate -
  it is drawn as the red marker rectangle AND used for validation and mask restriction.
- `remove` still describes the object (the marker + text combine); mention what to KEEP
  when similar objects are adjacent.
- `"mask": "region"` (optional): for big structural elements (walls, balconies) the
  model refuses the magenta recolor - this mode seeds the mask with the whole region
  rect and lets the inpaint diff carve out the object. Only works when the removal
  genuinely replaces the object (high contrast); check the preview extra carefully.
- `"must_cover": [[x,y,w,h], ...]` (optional): small zones the magenta pass tends to
  miss get unioned in via the inpaint diff. CAUTION: when the removal re-imagined the
  surroundings too, these rects pull in surrounding content as literal rectangles -
  prefer leaving fiddly attachments (plants on a ledge) in the bg instead: if the cat's
  feet can never overlap them without already being hidden by the main prop, excluding
  them is visually safe.

### 2. Run the tool

```bash
python tools/layer_room_props.py --manifest tools/layer_manifest_<case>_<room>.json
```

Backs up the untouched scene as `full scene.<ext>` next to `bg_base` (reruns read the
backup - idempotent), writes `prop_<id>.png` overlays + the new `bg_base`, and prints /
saves wiring suggestions to `debug_output/layering/<room>/report.json` (mask bbox,
`sortY` = mask bottom, a starter fade-trigger rect).

### 3. Review the images (do not skip)

Read `debug_output/layering/<room>/reconstruction.png` (must look identical to the
original), the new `bg_base` (inpaint quality where props were), and check overlay alpha
programmatically (opaque % small, bbox where expected). NOTE: reconstruction error alone
cannot catch a fully-opaque overlay - check the alpha channel, not just the composite.

### 4. Wire the room

1. `room_config.json` -> `props` (texture = content name, no extension):
   `{ "id": "prop_x", "texture": "prop_x", "sortY": <mask bottom - 4>, "triggerName": "prop_x_fade" }`
2. `room_map.json` -> add/extend an object layer named `Triggers` with a rect named
   `prop_x_fade` covering where the cat's FEET are when visually behind the prop
   (roughly mask bbox X +/- 10, from bbox top-ish down to `sortY - 15`). Bump
   `nextlayerid` / `nextobjectid`. (Adding Triggers is fine; never touch `Collisions`.)
3. `Content.mgcb` -> one entry per `prop_<id>.png` with `PremultiplyAlpha=False`
   (copy an existing prop block). `bg_base` already has an entry.
4. `dotnet build`, then verify:
   - `dotnet run --no-build -- --screenshot <case> <room>` - props composited = room
     looks unchanged.
   - Temporarily move `spawn_default` into a fade zone, screenshot again (prop faded,
     cat + anything hidden behind it visible), then REVERT the spawn.
   - `python tools/verify_level.py <case>`.

## Tuning / gotchas

- `sortY` is the prop's floor-contact Y: cat feet BELOW it draw in front, above = behind
  (and the prop jumps to LayerDepth 1.0 while fading so the cat shows through).
- If the mask call keeps grabbing a neighbouring object, shrink `region` and sharpen the
  "keep the other one" clause in `remove`.
- Overlapping props are not supported (masks are built independently against the
  original); pick non-overlapping objects or treat the cluster as ONE prop (the entrance
  console + fern is a single `prop_console`).
- Fill/overlay quality gates live as constants at the top of `layer_room_props.py`
  (`MIN_MASK_REGION_FRAC`, `MIN_REMOVED_DIFF`, ...).
- The "massive shadow" failure mode: a baggy mask (magenta covering floor around the
  object) makes the whole baggy area go translucent on fade. `refine_mask_with_fill`
  fixes this automatically - but it needs the removal to have KEPT the surroundings; if
  the fill re-imagined everything (big props), the refinement keeps ~100% and the mask
  is only as tight as the magenta. Check `preview_prop_*.png` for bagginess.
- Keep the bg-fill dilation (`fill_alpha`) tighter than the overlay's feathered edge,
  or re-imagined fill bleeds visibly past the prop onto kept content (tiles over a rug).
  A faint seam at the mask boundary can remain - it, and any inpaint weirdness, are
  ordinary Photoshop touch-ups on `bg_base` / the overlay PNG; the layering stays valid
  because overlays are original pixels.
- Old `bg_base` stays available as `full scene.<ext>` - to redo a prop, delete its
  overlay + its `debug_output/layering/<room>/prop_*_{magenta,removal}*.png` candidates
  and rerun with `--force`.
