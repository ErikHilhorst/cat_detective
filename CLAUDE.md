# Cat Detective — CLAUDE.md

## Project overview

A 2.5D isometric detective adventure game where a cat solves human crimes.
Cozy Studio Ghibli aesthetic, pre-rendered backgrounds, point-and-click/free-roam gameplay.
Built in C# using **MonoGame (KNI fork)**, targeting **Web/WASM** as the final platform.

Current state: the case system is playable end-to-end. The first case, **Malibu Mansion / "The Missing Macaw"**
(7 rooms, 34 clues), is fully wired with placeholder sprites: free-roam movement, room transfers,
interactable dialogue with keyword-unlocked clues, character interrogation menus (intro + selectable
cat-action topics, some evidence-gated), per-room deduction boards, and a final solve board.
Real art for interactables is still pending — `Shared/placeholder_person` / `placeholder_object` fill in.

---

## Tech stack

| Layer | Choice |
|-------|--------|
| Language | C# (.NET 8) |
| Framework | MonoGame.Framework.DesktopGL (dev) → KNI WASM (ship) |
| Map editor | Tiled — JSON export only |
| JSON parsing | System.Text.Json (built-in, no extra packages) |
| Content pipeline | MonoGame Content Builder (`.mgcb`) |

---

## File structure

```
CatDetective.csproj       — Project file; swap package for KNI to target WASM
Program.cs                — Entry point ([STAThread])
Game1.cs                  — Main loop; render passes, dialogue UI, journal/deduction UI, HUD, screenshot mode

Entities/
  GameObject.cs           — Abstract base: Position (bottom-center), LayerDepth, CollisionBox
  Prop.cs                 — Foreground furniture; fades when cat walks behind it
  Cat.cs                  — Player character; sprite-sheet animator, AABB movement
  Clue.cs                 — Clue + ClueCategory { Who, What, Why, WhereWhen }
  InteractionData.cs      — Dialogue text + keywords + texture fallback for an interactable
  InteractableEntity.cs   — In-world inspectable object (sprite + trigger rect)

Map/
  MapParser.cs            — Reads Tiled room_map.json: Collisions, Triggers, Transfers, Spawn, Interactables
  LevelConfigParser.cs    — Reads case_config.json / room_config.json
  SceneConfigParser.cs    — Per-case ambient color from scenes_config.json

Systems/
  NotebookManager.cs      — Clue database, unlock state, per-room counts, macro-clue queries
  DeductionManager.cs     — Mad-libs sentence parsing, slots, answer validation

tools/
  verify_level.py         — Static level checker (run from repo root, no game launch needed)

Content/
  Content.mgcb            — Asset pipeline config (ALL sprites: PremultiplyAlpha=False)
  scenes_config.json      — Per-case ambient light color
  Shared/                 — Cat sheets, dialogue font, UI art (notebook, tabs, dialogue box), placeholders
  Levels/<case_id>/
    case_config.json      — Clue database + rooms list + final solve board (runtime-read)
    <room_id>/
      room_map.json       — Tiled map (runtime-read, NOT pipeline-processed)
      room_config.json    — Interactable dialogue + local deduction board (runtime-read)
      bg_base.jpg         — Pre-rendered room background
      Interactables/      — Optional per-object sprites (falls back to `texture` in room_config)
```

---

## Architecture rules

### Pivot / origin — NEVER change this
All entities use **bottom-center** as the draw origin. `Position` = the floor contact point.
Changing this to top-left breaks Y-sorting for every entity in the scene.
`TextureOrigin` in `GameObject` computes `(width/2, height)` automatically.

### Layer depth formula — keep it normalised
```csharp
layerDepth = Math.Clamp(Position.Y / screenHeight, 0f, 1f);
```
`SpriteSortMode.FrontToBack` → higher depth = drawn in front. Do not invert this.

### Render pipeline — keep passes in order
| Pass | Blend | Content |
|------|-------|---------|
| 1 | AlphaBlend | `bg_base` at (0,0) |
| 2 | AlphaBlend | Blob shadow (before cat, so it sits under) |
| 3 | **NonPremultiplied**, FrontToBack | Cat + all props |
| 4 | **Additive** | `mask_sunbeams` at (0,0) |

Inserting a new entity: decide which pass it belongs to and draw it there.
New lighting overlays → Pass 4 (additive). New floor decals → Pass 1 or 2. New Y-sortable objects → Pass 3.

UI passes follow the world passes: Pass 6 = dialogue box, Pass 7 = HUD (SOLVE buttons, clue counters,
toasts), Pass 8 = journal/deduction board. UI layout constants live in reference space **2020×1136**
and are scaled to the current canvas in `UpdateLayout()` / with `jsx`/`jsy` in Pass 8.

### Text & font rules
- `Shared/dialogue_font.spritefont` covers **ASCII 32–126 only** (`DefaultCharacter` is `?` as a crash
  backstop). **All content JSON must be pure ASCII** — an em-dash or curly quote renders as `?`.
  Use ` - ` instead of `—` and straight quotes.
- Text never draws at raw font size inside a panel; it uses these scales (keep them consistent):
  - Dialogue box text: `0.72f`
  - Journal body (`jt`): `0.80f * jsy`; inspector text (`jti`): `0.66f * jsy` — the inspector
    description additionally **auto-shrinks** (`MeasureWrappedHeight`) until it fits the paper.
  - Buttons use `DrawUiButton`, which auto-fits the label to the rect.
- `_tabColors` (Who = yellow, What = green, Why = orange, WhereWhen = purple) **must match the
  pre-rendered tab-bar art** — slots, word-bank chips, and inspector headers all use this palette.

### Content pipeline — PremultiplyAlpha=False
Every sprite used in Pass 3 (NonPremultiplied) must be built with `PremultiplyAlpha=False` in `Content.mgcb`.
If a new sprite is added, add its entry to the `.mgcb` with this flag.
The background and sunbeam mask can use either setting; keep them False for consistency.

### Cat update — two-phase
`Cat.Update()` reads input and advances animation. It does NOT move the cat.
`Cat.MoveWithCollision()` applies velocity with per-axis AABB resolution.
Call them in that order from `Game1.Update()`. Do not merge them.

---

## Map / Tiled conventions

- Only **object layers** are parsed. Tile layers are ignored.
- Layer named `Collisions` → feeds `_solidBoundaries` (blocks cat movement).
- Layer named `Triggers` → fade zones; matched to props by name substring (`"desk"`, `"cabinet"`).
- Layer named `Transfers` → doorways; each object needs two custom properties: `TargetRoom` (folder name) and `TargetSpawn` (spawn point name in the target room).
- Layer named `Spawn` → named point objects. `spawn_default` is used on first entry; return spawns are named `spawn_from_<roomId>`.
- The JSON file is read at runtime with `File.ReadAllText`. Edit it without rebuilding `.mgcb`.
- Object rectangles only. Tiled polygons and ellipses are not supported yet.
- A layer-level `offsetx`/`offsety` on any layer shifts all its objects at parse time. The entrance Transfers layer uses offset `(-142, +192)` to align with the background art.

### Spawn point placement rule — critical
The cat collision box is **~61 px wide (±31 px from center; ~70 px / ±35 when facing up) and 32 px tall** (from `Position.Y - 32` up to `Position.Y`). A spawn point that lands inside a transfer zone causes an instant re-trigger loop. When placing a `spawn_from_X` point, ensure the entire collision box clears the transfer zone:

- **Y clearance**: `spawnY > zoneBottom + 32` (spawn below a zone) or `spawnY - 32 > zoneTop` adjusted accordingly
- **X clearance**: `spawnX - 35 > zoneRight` or `spawnX + 35 < zoneLeft` (spawn beside a zone)

A safe margin of ~15 px beyond the minimum is recommended.

### Case / room config keys

`case_config.json` (per case):
- `clues[]` — master clue database (`id`, `roomId`, `isMacroClue`, `category`, `name`, `context`, `inspectorDescription`).
- `rooms[]` — every room id in the case, in display order. Feeds `AllRoomsSolved` and the journal's solves overview. Keep in sync when adding rooms.
- `finalSolveSentence` — mad-libs template for the final board; `[bracketed]` tags become slots.
- `finalSolveClueIds[]` — answer clue ids matched to slots **in parse order**. A slot's category is taken from its answer clue, so tags can be readable text (`[Police Lockdown]`), not just `[WHO]`.

`room_config.json` (per room):
- `interactables[].texture` — fallback content path (e.g. `Shared/placeholder_person`) used when no per-name sprite exists under `Interactables/`. Combine with `scale` to size it.
- `interactables[].topics[]` — interrogation menu for **characters** (objects use `text` alone).
  `text` is the intro; after its last page a menu opens. Each topic:
  `{ "prompt": "<cat action>", "text": "<response, pages split on |>", "keywords": [...], "requiresClue": "<optional clue id>" }`.
  Intro keywords unlock on interaction; a topic's keywords unlock when the topic is chosen.
  Topics with `requiresClue` stay hidden until that clue is found. Visited topics render dimmed;
  the menu always ends with a fixed "Pad away. (Leave)" entry. Navigation: W/S or arrows + Enter.
- `localDeductionSentence` + `localDeductionClueIds[]` — same slot/answer scheme as the final board. Answers must be clue ids the room's own keywords can unlock **ungated** (intro, object, or non-gated topic — enforced by `verify_level.py`).

Deduction validation is enforced: a slot with a non-empty correct id rejects wrong clues ("Incorrect logic."). Solving a room stores its filled sentence for the journal recap and unlocks the room's macro clues.

### Clue-writing conventions (from playtest feedback — keep these)
- **Describe evidence, not conclusions.** `context`/`inspectorDescription` state facts and let the
  player connect them ("The cable vanishes into the wall, heading west"), never spell out the
  deduction ("the cable goes exactly where the hidden speaker was found").
- **Local solve sentences must not exonerate red herrings.** End on the suspicion, not the acquittal.
- **Name the cast.** Characters get real names in dialogue and on clue chips ("Rosa the Maid"),
  not bare archetypes.
- **Timetable clues are the good stuff.** Where a clue carries a time, put it in the clue `name`
  ("D. Marsh - 7:00 PM", "Dinner Cart - 7:00 PM") so the board itself becomes a timeline. Never
  reuse the same timestamp across two unrelated clues.
- **Decoys**: `isMacroClue: false` clues sharing a category with real answers pad local word banks
  without ever polluting the final board. Macro decoys (e.g. the 6:30 registry entry vs the 7:00
  manifest) create near-miss answers on the final board. Every flavor object should still yield
  *some* clue — "empty" interactions frustrated playtesters.
- **Balance categories per room** — a word-bank tab with 4+ clues while others are empty makes
  sorting tedious. Only the **final** board's word bank shows macro clues; a clue the player should
  see there must be `isMacroClue: true`.

### Dialogue-topic conventions (interrogation menus)
- **The cat cannot talk.** Topic prompts are cat actions ("Stare at him. Do not blink.",
  "Knock his phone off the side table"), never questions. Characters monologue in response —
  people explain themselves to cats. Prompts are bespoke per character; never reuse one.
- **3-4 topics per character.** Ungated topics carry the room's local answers and the
  personality; one gated topic (`requiresClue`) is the confrontation beat — the character
  reacting to evidence the player found. Gate with a same-room clue, or cross-room only for
  bonus depth (Derek's second rumble needs the kitchen roster; Petra's snatch theory needs the
  garden feathers).
- **Gated payoffs are slips and near-confessions, never solutions.** Basil admits the latch but
  not the theft; Marsh blurts "the cases are EMPTY, both of them"; the narrator never comments
  on what a slip means.
- **Cross-reference the cast.** Characters mention each other's timeline facts (Coco's squeaky
  wheels vs Derek's hall rumble; Wexler noting Marsh stopped complaining) so the timeline
  assembles across rooms.
- **Brackets always map to a keyword entry.** For emphasis inside a hint line, re-map the bracket
  to the clue it references (duplicate unlocks are harmless) rather than leaving it unmatched.

### Malibu Mansion — room wiring

```
entrance ──► living_room   (transfer right-side; spawn_from_entrance)
         └─► bedroom       (transfer right-side; spawn_from_entrance)

living_room ──► entrance   (transfer bottom-left; spawn_from_living_room)
            ├─► library    (transfer left-side;   spawn_from_living_room)
            ├─► kitchen    (transfer right-side;  spawn_from_living_room)
            └─► garden     (transfer top-center;  spawn_from_living_room)

bedroom ──► entrance       (transfer bottom-center; spawn_from_bedroom)

library ──► living_room    (transfer right-side; spawn_from_library)

kitchen ──► living_room    (transfer right-side; spawn_from_kitchen)

garden ──► living_room     (transfer right-side; spawn_from_garden)
       └─► pool_area       (transfer bottom-left; spawn_from_garden)

pool_area ──► garden       (transfer bottom-center; spawn_from_pool_area)
```

All connections are bidirectional. Background dimensions per room:
- `entrance`, `living_room`, `kitchen`: 1370 × 768
- `bedroom`, `garden`, `library`, `pool_area`: 1456 × 816

The canvas resizes automatically when switching between room sizes.

### "The Missing Macaw" — story bible (keep new content consistent with this)

**Cast**: Vivienne Vale (Movie Star), Derek (her Husband), Chip Sterling (Manager),
Officer Reyes (Police), Rosa (Maid), Basil (Gardener), Wexler (Director),
Petra (Bird Handler), Coco (Socialite), **D. Marsh a.k.a. the Sound Guy — the culprit**.
Rudebeak is the missing macaw.

**True timeline**:
- Early evening — Basil forces the cage latch with his pruning shears and frees Rudebeak into
  the garden (bird-bath feathers, bent shears). He freed the bird but did not take it.
- 6:30 PM — D. Marsh signs the entrance registry.
- 6:45–6:47 PM — Coco sees (and accidentally photographs) him hauling big black cases through
  the garden, where he grabs Rudebeak.
- 7:00 PM — He signs two sound-proof cases in at the bedroom (delivery manifest) while the
  production schedule has him in the library. Rosa's dinner cart is also in the bedroom wing at
  7:00 (duty roster) — that rumble is what Derek heard: the maid red herring.
- 8:00 PM — Fake squawk: hidden speaker under the cage, fed by the audio cable from the library
  mixing board. Motive: 47 takes ruined by the bird (session log).
- 8:15 PM — Police lockdown. Only the Police Lockdown clue carries the 8:15 timestamp.

**Red herrings**: Basil (freed the bird, acts guilty), Rosa (motive note + cart at 7 PM +
chicken feathers in the kitchen), Derek (headphones/ultimatum), Chip (PR panic — but he earns
commission on the bird), Wexler (thrilled about the drama).

**Final board answers** (parse order): `d_marsh_sound_guy` (WHO), `sound_proof_cases` (WHAT),
`d_marsh_7pm` (WHEN — decoy: `guest_registry` 6:30), `hidden_speaker` (HOW), `ruined_takes` (WHY).

---

## Build & run

```bash
dotnet restore
dotnet run
```

Controls: WASD or arrow keys. Enter to interact/advance dialogue. In an interrogation menu:
W/S (or arrows) to choose a topic, Enter to act. Escape to quit.

To rebuild content assets after changing `.mgcb`:
```bash
dotnet build   # MonoGame.Content.Builder.Task runs mgcb automatically
```

`room_config.json` / `case_config.json` / `room_map.json` are read at runtime — edit and re-run,
no `dotnet build` needed (the running game also hot-reloads room config).

### Screenshot mode (headless-ish visual verification)

```bash
dotnet run --no-build -- --screenshot <case_id> <room_id> [journal|final|dialogue]
```

Saves `debug_output/<case>_<room>.png` after 1.5 s and exits.
- *(no view arg)* — the room with HUD, placeholders, debug rects.
- `journal` — opens the local deduction board with the room's clues unlocked.
- `final` — opens the final solve board with all macro clues unlocked.
- `dialogue` — opens the room's **longest** text segment (intro or topic response), fully typed
  (text-box fit check).
- `topics` — opens the room's fullest interrogation menu with all gated topics unlocked
  (worst-case menu layout check).

### Static level checker

```bash
python tools/verify_level.py   # run from the repo root
```

Validates without launching the game: interactable reachability vs collisions (cat feet box),
transfer ↔ spawn wiring (12 checks for Malibu), spawn clearance from transfer zones, config
consistency (keyword ids exist, local/final answers present & discoverable), and final-board slot
validity. Run it after any content change.

---

## Switching to KNI / WASM

In `CatDetective.csproj`, replace:
```xml
<PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.1.303" />
<PackageReference Include="MonoGame.Content.Builder.Task"  Version="3.8.1.303" />
```
with the appropriate KNI platform packages. No C# source changes are needed.

---

## What NOT to do

- Do not use `SpriteSortMode.BackToFront` — the entire depth system assumes `FrontToBack`.
- Do not set `PremultiplyAlpha=True` for Pass 3 sprites — it will cause darkening when props fade.
- Do not parse Tiled tilesets — the GDD uses pre-rendered PNGs, not tile-based rendering.
- Do not add collision logic inside `Cat.Update()` — keep movement and collision in `MoveWithCollision()`.
- Do not commit art assets to git as large binaries — use Git LFS or keep them out of the repo.
- Do not put non-ASCII characters (em-dashes, curly quotes, ✓) in content JSON — the sprite font
  only covers ASCII 32–126.
- Do not edit `Collisions` layers in room maps — the user tunes collision boxes himself in Tiled.
- Do not spell out deductions in clue text — see "Clue-writing conventions".
