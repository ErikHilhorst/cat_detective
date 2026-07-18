---
name: generate-object-sprites
description: Generate in-game object sprites end-to-end via the Gemini image API (nano banana). Builds/extends a sprite manifest from room configs, runs tools/generate_object_sprites.py (generate, white-background cutout, trim, resize, place in Interactables/), wires Content.mgcb entries, sets room_config scales, and verifies via screenshot mode.
---

# Generate object sprites

Automates the manual pipeline the user used to do by hand: prompt Gemini for object art,
cut out the background, resize, save under the right room's `Interactables/` folder, and
wire the content pipeline. Handles both **object** interactables and **character**
interactables: mark a manifest entry with `"character": true` and it uses the manifest's
`character_prompt_template` instead (full body, head kept in the upper part of the frame
so the dialogue-portrait crop - top 32% x middle 60% - lands on the face). `--scan` tags
`placeholder_person` entries as characters automatically.

## Prerequisites

- `GEMINI_API_KEY` in `.env` at the repo root (gitignored) or the environment.
- Python with Pillow + requests (both verified installed).
- The object must already exist as an interactable: an entry in the room's
  `room_config.json` and an object in the room's Tiled `Interactables` layer
  (use the `add-clue-interactable` skill first if it does not).

## Workflow

### 1. Ensure a manifest entry exists

Manifests live at `tools/sprite_manifest_<case>.json` (Malibu: `tools/sprite_manifest_malibu.json`,
complete for all 25 objects + 1 intentionally skipped). Entry format:

```json
{ "id": "inspect_birdcage", "room": "living_room", "size": 123,
  "prompt": "An ornate empty brass birdcage on a stand, ..." }
```

- `size` = target on-screen pixel size (max of width/height after cutout). Derive it from
  the object's current placeholder tuning: `round(1024 * scale)` (the shared placeholder
  is 1024 px square). For a brand-new object with no tuned scale, estimate against room
  furniture: papers/small props ~70-85, medium props ~90-110, large set pieces ~120-140.
  Characters: estimate against the cast instead - Dikkie is ~100 px tall, adult humans
  ~230-300; the `1024 * scale` formula does NOT apply to `placeholder_person` entries,
  so always hand-pick character sizes.
- `"character": true` = use `character_prompt_template` (full-body, portrait-safe head
  placement). The prompt should still describe pose explicitly; for non-upright poses
  (a sleeping animal) keep the head toward the top of the composition or the dialogue
  portrait crops to fur.
- `prompt` = one or two sentences describing ONLY the object, matching the clue's dialogue
  text in `room_config.json`. Pure ASCII. The manifest's `prompt_template` wraps it with
  the Ghibli style + white-background boilerplate; do not repeat that in the prompt.
- **Never place the object in an environment.** Phrases like "lying on the floor",
  "on the ground", "on a table", "pinned to a fridge/wall" make the model paint that
  surface as the background instead of leaving it white (confirmed by the user's manual
  runs: a plain calendar came out clean, "a calendar lying on the floor" filled the
  background in). Describe pose and orientation instead: "palm up, fingers curled",
  "with a pushpin through one corner", "held by a small magnet at its top edge".
  Attached supports (a tripod, a cage stand, a clipboard) are fine - they are part of
  the object.
- `"skip": true` excludes an entry (e.g. things painted into the background).

For a new case, print a skeleton (ids, rooms, sizes prefilled from placeholder scales) and
fill in the prompts:

```bash
python tools/generate_object_sprites.py --scan <case_id> > tools/sprite_manifest_<case>.json
```

### 2. Generate

Run from the repo root:

```bash
python tools/generate_object_sprites.py --manifest tools/sprite_manifest_<case>.json --write-mgcb
```

Useful flags:
- `--only id1,id2` — subset. `--dry-run` — list planned work.
- Existing outputs are skipped by default; `--force` regenerates (new API call).
- `--reprocess` — no API call; re-run cutout/trim/resize on the saved raw images in
  `debug_output/sprite_raw/`. Combine with `--tolerance` when tuning the cutout.
- `--tolerance N` (default 235) — whiteness floor for the background flood fill.
  Lower it (e.g. 215) if faint shadows/off-white halos survive around sprites.
- `--write-mgcb` — append the standard `PremultiplyAlpha=False` block to
  `Content/Content.mgcb` for any generated sprite missing an entry (idempotent).

The cutout flood-fills from the image borders only, so white/cream regions inside an
object (stationery, paper props) are preserved.

### 3. Set the in-game scale to 1.0

Generated sprites are saved **pre-sized** to their target on-screen size, so each
processed object's `"scale"` in its `room_config.json` must become `1.0` (the old value
was tuned for the 1024 px placeholder and would render the new sprite microscopic).
Keep the `"texture"` fallback line — it is harmless and covers missing files.
`room_config.json` hot-reloads, but new textures do not: step 4 is still required.

### 4. Build and verify

```bash
dotnet build          # runs mgcb; fails loudly if a .png listed in Content.mgcb is missing
python tools/verify_level.py
dotnet run --no-build -- --screenshot <case_id> <room_id>
```

Read the produced `debug_output/<case>_<room>.png` and check each sprite: correct object,
clean cutout (no white halo, no clipped edges), sensible size against the furniture.
Screenshot every affected room.

### 5. Fix problems

| Problem | Fix |
|---|---|
| White halo / faint shadow ring | `--reprocess --tolerance 215 --only <id>` (no API cost) |
| Holes punched in a white-ish object | raise `--tolerance` (e.g. 245) and `--reprocess` |
| Wrong/ugly object | tweak the manifest prompt, rerun with `--force --only <id>` |
| Object too big/small in room | adjust that object's `size`, then `--reprocess --only <id>` (hot-reload shows scale changes live, sprite size needs `dotnet build` + restart) |
| Background filled in with a floor/scene instead of white | remove environment placement from the prompt ("lying on the floor", "on a table", "pinned to a wall") - describe pose instead; regenerate with `--force` |
| Multiple objects in one image | add "exactly one" phrasing to the prompt; regenerate with `--force` |
| API 429 / quota | script retries with backoff; if it still fails, rerun later - completed sprites are skipped automatically |

## Rules

- Every generated sprite MUST have a `Content.mgcb` entry with `PremultiplyAlpha=False`
  (Pass 3 is NonPremultiplied). `--write-mgcb` emits the correct block.
- Output path is fixed by the loader: `Content/Levels/<case>/<room>/Interactables/<id>.png`
  where `<id>` is the Tiled object name. Never rename to something prettier.
- Raw generations stay in `debug_output/sprite_raw/` (gitignored) so cutout parameters can
  be retuned without paying for regeneration. Do not commit them.
- Manifest prompts are content: pure ASCII, describe evidence not conclusions (a torn log,
  not "the culprit's torn log").
