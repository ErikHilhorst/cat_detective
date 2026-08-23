# Cat Detective — CLAUDE.md

## Project overview

A 2.5D isometric detective adventure game where a cat solves human crimes.
Cozy Studio Ghibli aesthetic, pre-rendered backgrounds, point-and-click/free-roam gameplay.
Built in C# using **MonoGame (KNI fork)**, targeting **Web/WASM** as the final platform.

Current state: the case system is playable end-to-end AND the game has its shell: a main menu
(CONTINUE / NEW GAME / TUTORIAL / SETTINGS / QUIT), a single-slot autosave, a settings screen
(music volume + CRT filter toggle, persisted), a poster-and-typewriter case intro, a five-beat
end scene, and a two-room tutorial case. The protagonist's name is **Dikkie**.

The first case, **Malibu Mansion / "The Missing Macaw"**
(7 rooms, 74 clues: 65 room clues + 9 case-global TIME clues), is fully wired: free-roam movement, room transfers,
interactable dialogue with keyword-unlocked clues, character interrogation menus (intro + selectable
cat-action topics, some evidence- or solve-gated), per-room deduction boards, and a final solve board.
All ten character sprites and all object sprites are real art (per-name PNGs under each room's
`Interactables/`, built via `.mgcb`). The old `inspect_pool_water` placeholder was replaced by
`inspect_feather` (pool_area) - a bright feather lying on the deck below the pool; the
`feather_in_filter` clue id was kept for save-compat. `rudebeak.png` is wired as the end
scene's beat-4 reveal.
Prompts for the missing object art (two 5x2 sheets): `Levels/malibu_mansion/sprite_prompts.txt`.
The entrance has two walk-behind foreground props (`prop_console` - hides the guest
registry until it fades - and `prop_outer_wall`, the windowed exterior wall in the
lower-left, whose ledge planters deliberately stay in the bg), generated from the flat
bg via `tools/layer_room_props.py` + the `layer-room-props` skill; the untouched scene is
kept as `entrance/full scene.jpg`. The living room uses hand-layered art: `bg_base.jpg` is the
furniture-free repaint (source `base_layer.jpg`) plus four full-canvas walk-behind props
(`prop_table`, `prop_north_couch`, `prop_south_couch`, `prop_sofa_chair`) with fade zones in the
map's Triggers layer; the birdcage sprite (`Interactables/inspect_birdcage.png`) is cropped from
the layered `cage.png` and Y-sorts on its own as an interactable (no fade).

The second case, **tutorial / "Whisker Academy - Hall of Basics"** (2 rooms `lesson_one` /
`lesson_two`, 12 clues), is Dikkie's dream of his training: a white void with placards that teach
movement, clues, interrogation menus, decoys, local solves, doorways, confrontations, and the
final board - built entirely on the normal case systems (no special tutorial code paths) with
`Shared/placeholder_object` / `placeholder_person` sprites and flat near-white `bg_base.jpg`s.

### Game states & shell

`Game1.GameState`: `MainMenu, Settings, CaseIntro, Playing, EndScene, DevMenu`. Boot = MainMenu.

- **MainMenu**: W/S + Enter or mouse. CONTINUE only enabled while a save exists; NEW GAME over an
  existing save asks for a second Enter ("overwrite" confirm); TUTORIAL starts the tutorial case;
  F12 opens the legacy DevMenu scene picker; Esc quits. Menus run at a fixed 1456x816 canvas
  (`SetCanvas`); rooms still resize the canvas per background.
- **Escape is contextual**: in Playing it closes topic menu/dialogue first, then the board, then
  autosaves and returns to the menu. It only quits the app from the main menu.
- **Save system** (`Systems/SaveSystem.cs`): single slot at
  `%LocalAppData%/CatDetective/save.json` (+ `settings.json`). AutoSave fires on room transfer,
  local solve, and Esc-to-menu; the final solve deletes the save (case over). Restore =
  `LoadCase` + replay `UnlockClue` per saved id (`_isRestoring` suppresses toasts) + copy
  solved/visited state + `LoadRoom(saved room)`. All IO failure-tolerant; swap file bodies for
  the KNI/WASM port.
- **CaseIntro** (`StartCaseIntro`): poster (`Shared/case_poster`, an .mgcb output-rename of
  `Shared/case of the mising maccaw.jpg` - keep the typo'd source filename) + typewriter pages
  from `Systems/CaseScripts.cs`. Enter completes/advances, Esc skips into the case. Cases with
  no authored intro skip straight to `LoadCase`. Keep intros to ONE short page, light on noir
  (playtest 5); the typewriter starts after a short beat (negative `_typewriterTimer`) so the
  poster lands first.
- **EndScene** (`StartEndScene`): beats from `CaseScripts.GetEndScene(caseId)`; a beat can show
  `rudebeak.png` (`ShowRudebeak`) or render as a centered title card (`IsCard`). Replaces the old
  `_isGameWon` win banner entirely. Reached from a valid final-solve submit.
- **Settings**: music volume (10 cells, applies to `MediaPlayer.Volume` live) + CRT toggle;
  persisted on every change. Music now starts in `LoadContent` (the menu has music).
- **CRT filter** (`Systems/CrtOverlay.cs`): shader-free scanlines + vignette, drawn INTO the
  render target as Pass 9 - deliberately no .fx/EffectProcessor (KNI/WASM portability).

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

Systems/ (shell)
  SaveSystem.cs           — Single-slot save + settings persistence (%LocalAppData%/CatDetective/)
  CaseScripts.cs          — Intro pages + end-scene beats per case id (all epilogue/intro copy)
  CrtOverlay.cs           — Shader-free CRT (scanline strip + vignette), Pass 9
  RoomPreloader.cs        — Background asset warmer: queues every room's textures at boot
                            (saved room first, then cases in scene order) and loads ONE per
                            frame from Update(), only on frames where a blocking load can't
                            be felt (non-Playing states, dialogue/board open, or no movement
                            key held). Exists for the web build, where each Content.Load is
                            a synchronous XHR + Brotli decode (~10 s per cold room);
                            LoadRoom itself is unchanged - the shared ContentManager caches
                            by asset path, so a warmed room loads as pure cache hits.

Map/
  MapParser.cs            — Reads Tiled room_map.json: Collisions, Triggers, Transfers, Spawn, Interactables
  LevelConfigParser.cs    — Reads case_config.json / room_config.json
  SceneConfigParser.cs    — Per-case ambient color from scenes_config.json

Systems/
  NotebookManager.cs      — Clue database, unlock state, per-room counts, macro-clue queries
  DeductionManager.cs     — Mad-libs sentence parsing, slots, answer validation

tools/
  verify_level.py         — Static level checker (run from repo root, no game launch needed)
  generate_object_sprites.py — Gemini sprite generator (see generate-object-sprites skill)
  layer_room_props.py     — Walk-behind prop layer generator (see layer-room-props skill)

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
Pass 9 (optional, `_settings.CrtEnabled`) draws the CRT overlay INTO the render target after all
other passes and in every state - keep new passes before it.

Pass 4b (NonPremultiplied, world-anchored) draws a bobbing `Shared/speech_bubble` sprite over
characters that have available, unheard topics — hidden while dialogue is open. The same pass
draws a permanent `Shared/arrow` paw-print arrow (art points right; left mirrors, up/down
rotate a quarter turn; direction from the zone's position vs room center) over every transfer
zone so exits are discoverable without the F1 overlay. While the cat stands in a transfer zone
its arrow turns gold and pulses (same cue language as the SOLVE button), alongside the
"[ Enter ] to enter" prompt. Bubble = "someone to talk to", arrow = "exit" — keep the two
sprites visually distinct and small (bubble 40 px, arrow 64 px).
UI passes follow the world passes: Pass 6 = dialogue box, Pass 7 = HUD (SOLVE buttons, clue counters,
toasts), Pass 8 = journal/deduction board. UI layout constants live in reference space **2020×1136**
and are scaled to the current canvas in `UpdateLayout()` / with `jsx`/`jsy` in Pass 8.
The casebook has two spreads: Pass 8 is the deduction board (`ui_notebook_bg`), Pass 8b is the
**Case Notes** page (`ui_notebook_score_card`, `_journalOnNotesPage`) holding the investigation
score card - room checklist + clue counts, case clue total, confronted count, and each solved
room's recap sentence (`_roomSolvedSentences`). "CASE NOTES >" (bottom-right of the board) turns
forward, "< BOARD" (bottom-left) turns back; Esc backs out one layer (notes -> board -> close).
Progress readouts live ONLY on the notes page - keep the deduction spread free of counters.
The notes page also draws **"The Evening"** under the room checklist: every found clue whose
name carries a timestamp - i.e. the TIME clues - in chronological order (`TryParseClueTime`,
auto-shrinks to fit). The WHERE/WHEN word-bank tab sorts its chips the same way
(`SortCluesByTime`; untimed event chips keep discovery order, after the times) - so the tab
reads as the evening's known timeline. Local word banks draw from `GetLocalBankClues`
(room clues + found case-global clues).
Neither bg has baked titles (the board art keeps only the inspector-panel paper); ALL headers
are in-engine ink text: the board's left page shows the room name over "What happened here?"
(-> green "Solved!" once solved; final board = "The Final Solve" / "What really happened?"),
the notes page draws "The Investigation" / "Deductions so far". Keep new titles in-engine.

### Text & font rules
- `Shared/dialogue_font.spritefont` covers **ASCII 32–126 only** (`DefaultCharacter` is `?` as a crash
  backstop). **All content JSON must be pure ASCII** — an em-dash or curly quote renders as `?`.
  Use ` - ` instead of `—` and straight quotes.
- Dialogue/topic `text` may contain `\n` — `DrawRichText` treats it as a forced line break
  (bulleted lists, radio transmissions). Keep bullets to `- ` (ASCII).
- **Quoted speech starts on a new line (playtest 5).** Whenever a character's spoken quote
  begins mid-text (after sentence punctuation or a colon), put a `\n` before the opening `'`.
  Prefer more pages over walls of prose. Mid-sentence attributions (`,' she says, 'and...`)
  stay inline. `tools/`-side one-off: the regex `([.!?:]) '` -> `$1\n'` applied this
  case-wide; follow the rule by hand for new content.
- **Auto-pagination**: `PaginateDialogue` splits on author `|` first, then auto-splits any page
  that would overflow the box's text area — at `\n` boundaries, then sentence ends, never inside
  a `[keyword]`. Long texts can no longer overflow the box, but still prefer hand-placed `|`
  breaks at narrative beats (2-3 sentences per page).
- **Portraits**: every interactable shows a portrait beside the dialogue text, from its own
  in-world sprite (`SetDialoguePortrait`). Characters (interactables with `topics`) use the
  top 32% x middle 60% face crop; objects show their full sprite. `portraitCrop` in
  room_config overrides either default. The text origin shifts right by
  `PORTRAIT_MAX_W + PORTRAIT_TEXT_GAP`; pagination budgets the same shift, so keep those
  constants in sync.
- The dialogue box shows a small **name label** top-left: the interactable's `name` field,
  falling back to the prettified id (`inspect_duty_roster` -> "Duty Roster"). Give objects a
  `name` when the prettified id reads badly.
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
- Layer named `Transfers` → doorways; each object needs two custom properties: `TargetRoom` (folder name) and `TargetSpawn` (spawn point name in the target room). Optional: `ArrowDirection` (overrides the doorway arrow heading), `RequiresVisited` (room id the player must have entered once before this door opens - linear-order gating; its arrow draws dim grey and Enter shows a toast until then) and `LockedText` (that toast's text; keep it short, toasts are one line). `verify_level.py` checks `RequiresVisited` names a real room.
- Layer named `Spawn` → named point objects. `spawn_default` is used on first entry; return spawns are named `spawn_from_<roomId>`.
- The JSON file is read at runtime with `File.ReadAllText`. Edit it without rebuilding `.mgcb`.
- Object rectangles only. Tiled polygons and ellipses are not supported yet.
- A layer-level `offsetx`/`offsety` on any layer shifts all its objects at parse time. The entrance Transfers layer uses offset `(-142, +192)` to align with the background art.

### Spawn point placement rule — critical
The cat collision box is **~49 px wide (±25 px from center; ~56 px / ±28 when facing up) and 32 px tall** (from `Position.Y - 32` up to `Position.Y`; derived from the 350 px frame at scale 0.28/0.32 in `Cat.cs`). `verify_level.py` deliberately checks with a wider 70 px box as a safety margin. A spawn point that lands inside a transfer zone causes an instant re-trigger loop. When placing a `spawn_from_X` point, ensure the entire collision box clears the transfer zone:

- **Y clearance**: `spawnY > zoneBottom + 32` (spawn below a zone) or `spawnY - 32 > zoneTop` adjusted accordingly
- **X clearance**: `spawnX - 35 > zoneRight` or `spawnX + 35 < zoneLeft` (spawn beside a zone)

A safe margin of ~15 px beyond the minimum is recommended.

### Case / room config keys

`case_config.json` (per case):
- `clues[]` — master clue database (`id`, `roomId`, `isMacroClue`, `category`, `name`, `context`, `inspectorDescription`).
  `roomId: ""` = **case-global clue** (the time clues): shows in every room's word bank once
  found, belongs to no room's counter, counts as a slot option everywhere.
- `rooms[]` — every room id in the case, in display order. Feeds `AllRoomsSolved` and the journal's solves overview. Keep in sync when adding rooms.
- `finalSolveSentence` — mad-libs template for the final board; `[bracketed]` tags become slots.
- `finalSolveClueIds[]` — answer clue ids matched to slots **in parse order**. A slot's category is taken from its answer clue, so tags can be readable text (`[Lockdown]`), not just `[WHO]`.

`room_config.json` (per room):
- `interactables[].texture` — fallback content path (e.g. `Shared/placeholder_person`) used when no per-name sprite exists under `Interactables/`. Combine with `scale` to size it.
- `interactables[].interactPadding` — extra reach in px added on every side of the Tiled rect
  for the interaction/highlight check ONLY (`InteractableEntity.InteractZone`). Placement,
  Y-sort, and drawing still use the raw rect. Use for counter-top objects the cat can't
  physically stand next to (kitchen counter items use 60). `verify_level.py` honors it.
- `interactables[].alwaysOnTop` — draws the sprite at LayerDepth 1, in front of the cat and
  every prop, instead of Y-sorting. For hero objects that must never be occluded (the living
  room birdcage). Use sparingly - it breaks depth illusion for anything the cat should be
  able to walk in front of.
- `interactables[].spriteAnchor` — where the sprite sits INSIDE the Tiled rect, CSS
  background-position style: `TopRight` tucks the sprite flush against the rect's top and
  right edges. Values: `BottomCenter` (default = classic rect bottom-center), `TopLeft`,
  `TopCenter`, `TopRight`, `LeftCenter`, `Center`, `RightCenter`, `BottomLeft`,
  `BottomRight`. The alternative to interactPadding: draw ONE large box (whole box = the
  interact area) reaching over walkable floor, and anchor the sprite into the corner of the
  box that covers the unreachable furniture. Y-sort follows the sprite's resulting bottom
  edge. Fine-tune with `offsetX`/`offsetY` (draw-only, no depth change).
- `interactables[].revealName` + `revealNameOnClue` — dialogue-label name swap once the reveal
  clue is unlocked (The Sound Guy -> "D. Marsh" after `d_marsh_sound_guy`). Toasts deliberately
  keep the pre-reveal `name` (anti-leak rule).
- `interactables[].altText` (+ `altTextRequiresClue` / `altTextRequiresSolve`, AND-combined) —
  alternate intro shown once its gates are satisfied, so characters react to investigation
  progress instead of repeating their cold intro. The regular `keywords` still unlock and
  highlight either way, so alt intros never affect clue discoverability; every `[bracket]` in
  `altText` must match an entry in the interactable's `keywords`.
- `interactables[].topics[]` — interrogation menu for **characters** (objects use `text` alone).
  `text` is the intro; after its last page a menu opens. Each topic:
  `{ "prompt": "<cat action>", "text": "<response, pages split on |>", "keywords": [...], "requiresClue": "<optional clue id>", "requiresSolve": "<optional room id>" }`.
  Intro keywords unlock on interaction; a topic's keywords unlock when the topic is chosen.
  Topics with `requiresClue` stay hidden until that clue is found; topics with `requiresSolve`
  stay hidden until that room's local board is solved (both set = AND). Visited topics render
  dimmed; the menu always ends with a fixed "Pad away. (Leave)" entry. Navigation: W/S or arrows + Enter.
- `localDeductionSentence` + `localDeductionClueIds[]` — same slot/answer scheme as the final board. Answers must be clue ids the room's own keywords can unlock **ungated** (intro, object, or non-gated topic — a gated answer would be circular; enforced by `verify_level.py`).

Deduction validation is enforced: a wrong submit reports partial progress ("Incorrect logic - 2/3 fit.")
so a failed solve is a deduction step, not trial-and-error. Solving a room stores its filled sentence
for the journal recap, unlocks the room's macro clues, and unlocks any `requiresSolve` confrontation
topics gated on that room (with a queued "X might have some explaining to do..." toast).

### Clue-writing conventions (from playtest feedback — keep these)
- **Describe evidence, not conclusions.** `context`/`inspectorDescription` state facts and let the
  player connect them ("The cable vanishes into the wall, heading west"), never spell out the
  deduction ("the cable goes exactly where the hidden speaker was found").
- **Local solve sentences must not exonerate red herrings.** End on the suspicion, not the acquittal.
- **Name the cast.** Characters get real names in dialogue and on clue chips ("Rosa the Maid"),
  not bare archetypes.
- **Times are their own clues (playtest 5 - the core timeline mechanic).** Every timestamp is a
  separate case-global clue: `time_700pm` = name "7:00 PM", `roomId: ""`, category WhereWhen,
  macro (only `time_545pm` is micro). Event clues carry NO time in their name, context, or
  inspector text ("Dinner Cart", "Manifest Entry", "Lockdown") - the SOURCE dialogue states the
  pairing once (`[dinner cart, east wing - R.]` next to `[7:00 PM]`, each bracket its own
  clue), and the player re-pairs event and time on the boards. That re-pairing IS the timeline
  deduction; revisiting a source to re-read a time is intended play. Global clues appear in
  every room's word bank once found (`GetLocalBankClues`), belong to no room's counter, and
  count as slot options in every room (verify_level.py). Never create two time clues for the
  same minute, and keep non-clue times out of prop text where they could read as evidence
  (the registry's per-guest arrival times were cut for this; manifest chaff entries like
  "Linen return - 7:10 PM" are deliberate noise).
- **One clue = one fact (playtest 4).** A clue name never carries the interpretation a board
  asks for: "Heavy Cases" pre-answered the bedroom board, so it became "Manifest Entry" with
  the illegible signature split into its own clue (`scrawled_signature`). A fact with two
  implications gets two clues (the selfie vs the straw hat at its edge).
- **Board sentences pose the tension, never the answer (playtest 4).** A solve sentence must not
  quote a timestamp, document name, or distinctive noun that appears in an answer chip's name or
  its discovery text (old entrance board: "sign-in book ... from 6:30 PM on" vs a chip literally
  named "Crew Arrivals - 6:30 PM" = string match, zero deduction). State the contradiction and
  let chips resolve it; same-category slots are ordered chronologically by the sentence. Slot
  `[tags]` never render in-game (empty slots show only "[ WHO ]" etc.), so write tags as short
  author-facing labels.
- **Decoys**: `isMacroClue: false` clues sharing a category with real answers pad local word banks
  without ever polluting the final board. Macro decoys (e.g. the 6:30 registry entry vs the 7:00
  manifest) create near-miss answers on the final board. Every flavor object should still yield
  *some* clue — "empty" interactions frustrated playtesters.
- **Balance categories per room** — a word-bank tab with 4+ clues while others are empty makes
  sorting tedious. Only the **final** board's word bank shows macro clues; a clue the player should
  see there must be `isMacroClue: true`.
- **No single-option solve slots.** Every category a local board uses must have at least one decoy
  clue in that room, or the slot is an auto-fill instead of a deduction (playtest 2 finding;
  `verify_level.py` warns). Prefer decoys that reinforce a red herring or the timeline.

### Dialogue-topic conventions (interrogation menus)
- **The cat cannot talk.** Topic prompts are cat actions ("Stare at him. Do not blink.",
  "Knock his phone off the side table"), never questions. Characters monologue in response —
  people explain themselves to cats. Prompts are bespoke per character; never reuse one.
- **3-5 topics per character.** Ungated topics carry the room's local answers and the
  personality; gated topics are the confrontation beats — the character reacting to evidence
  (`requiresClue`) or to being cornered by the player's deduction (`requiresSolve`). Clue-gate
  with a same-room clue, or cross-room only for bonus depth (Petra's snatch theory needs the
  garden feathers).
- **Solve-gated confrontations.** Each room's key character has a `requiresSolve` topic that
  rewards solving that room's board: Reyes (entrance), Vivienne (living_room), Derek (bedroom),
  Rosa (kitchen), Basil (garden), Marsh (library). The payoff follows the slip rules below - a
  red herring cracks their alibi or redirects suspicion; the culprit near-confesses. This makes
  the SOLVE button an active investigative tool, not a checklist (playtest 2 finding).
- **FINAL SOLVE confrontation lock (playtest 3).** The final board opens only when every room
  is solved AND every `requiresSolve` topic has been heard (`_confrontationTopics`, built in
  `BuildGateIndex`; `AllConfrontationsHeard`). The button shows "M/N confronted" between those
  states, and the locked toast names who still owes an explanation. Adding a `requiresSolve`
  topic automatically adds it to the lock.
- **HUD progress cues (playtest 3).** The SOLVE button pulses gold once every clue in the room
  is found and turns muted green "SOLVED" after the room's board is solved - players kept
  leaving rooms without solving them. Confrontation clues (only unlockable via gated topics -
  `_ungatedClueIds` built in `BuildGateIndex`) do NOT hold the pulse back, and the room clue
  counters (HUD + notes checklist) hide them from the total until found
  (`GetRoomClueCounts(roomId, hideWhileLocked)`) - so SOLVE never pulses at "6/7"
  (playtest 5 finding).
- **Confrontation clues (playtest 4 rework).** Gated topics MAY carry unique clues - each
  confrontation slip is recorded as its own clue id so the notebook keeps the late-game record
  (`hall_rumble_no_soup` from Derek, `cable_grease` from Rosa, `knew_cases_empty` from Marsh).
  Rules: (1) local AND final answers always need an ungated source (local: same room;
  confrontation clues are evidence and near-miss decoys, never answers); (2) a gated clue's
  gates should be satisfiable in its own room (requiresClue on an in-room clue, or
  requiresSolve on the room itself) - a cross-room gate (`knew_cases_empty` needs the garden
  cases) makes the room counter need a detour, and `verify_level.py` WARNs.
- **Confrontation payoffs stay neutral (playtest 5).** A confrontation reward never points at
  the culprit ("one signature landed half an hour before its call sheet" was cut) - it
  sharpens the question, not the answer: Reyes's registry hunch ("somebody's evening in this
  book does not add up" = `registry_doesnt_add_up`) is UNGATED (it is an entrance board answer);
  his solve-gated payoff is the canvass slip `unanimous_at_eight` - everyone heard the same
  squawk at the same minute, and "honest witnesses never agree that neatly." The pointed
  arithmetic lives on the final board itself.
- **Gated payoffs are slips and near-confessions, never solutions.** Basil admits the latch but
  not the theft; Marsh blurts "the cases are EMPTY, both of them"; the narrator never comments
  on what a slip means.
- **Toasts: solve-gated only (playtest 5).** Clue-gate unlock toasts ("X might have some
  explaining to do..." on finding a requiresClue gate clue) were REMOVED - they told the
  player where to go before they could choose. Only SOLVING a room fires that toast, for the
  `requiresSolve` topics the solve unlocks - an earned reward pointer (via `_solveGateIndex`,
  scanned in `BuildGateIndex`). Everything else is discovered through the speech-bubble
  indicator. Character names come from the interactable's optional `name` field (fallback:
  prettified id); keep D. Marsh's as "The Sound Guy" so a toast can't leak his name early.
  Toasts are **queued** and the timer holds while the dialogue box is open.
- **Unheard-topic indicator.** Characters with at least one available (gate satisfied) topic the
  player has not heard show a bobbing speech bubble above their trigger zone (Pass 4b,
  `HasUnseenTopics`). It disappears when every available topic is visited and reappears when a
  gate unlocks a new one. The toast says *who* to revisit across rooms; the "..." says *here*.
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
        └─► garden         (transfer west-door;  spawn_from_kitchen)

garden ──► living_room     (transfer right-side;    spawn_from_garden)
       ├─► kitchen         (transfer right-edge;    spawn_from_garden)
       └─► pool_area       (transfer bottom-left;   spawn_from_garden)

pool_area ──► garden       (transfer bottom-center; spawn_from_pool_area)
```

All connections are bidirectional. **Forced opening order**: the entrance -> living_room door
carries `RequiresVisited: bedroom` (Reyes: "Photos in progress. East wing first, whiskers."),
so the player meets Derek and his 8 PM squawk-through-headphones testimony BEFORE the living
room - the false "bird alive at 8" anchor is built in the bedroom, then demolished at the
cage. From the living room on, movement is free. Visited rooms persist in the save
(`visitedRooms`; older saves seed from the saved room + solved rooms).

Background dimensions per room:
- `entrance`, `living_room`, `kitchen`: 1370 × 768
- `bedroom`, `garden`, `library`, `pool_area`: 1456 × 816

The canvas resizes automatically when switching between room sizes.

### "The Missing Macaw" — story bible (keep new content consistent with this)

**Cast**: Vivienne Vale (Movie Star), Derek (her Husband), Chip Sterling (Manager),
Officer Reyes (Police), Rosa (Maid), Basil (Gardener), Wexler (Director),
Petra (Bird Handler), Coco (Socialite), **D. Marsh a.k.a. the Sound Guy — the culprit**.
Rudebeak is the missing macaw.

**True timeline** (the gardener and the sound guy interlock — revealed only through clues):
- Just before 6:00 PM — Basil forces the cage latch with his pruning shears, meaning to free
  Rudebeak. The front door goes (crew arriving early); he panics, drops one glove under the cage
  (Dropped Glove), and bolts to the garden to hide behind the potting shed. He never opened the
  cage door.
- ~6:00 PM — Rudebeak lets himself out through the broken latch and heads for his evening splash
  in the garden bird bath (missed 6:00 snack, evening feathers).
- 6:30 PM — the crew column in the entrance registry begins (`time_630pm` unlocks here); one
  crew member was inside half an hour before his own 7:00 call, but NOTHING in the game states
  that arithmetic. Reyes states his registry hunch ungated ("somebody's evening in this book
  does not add up", `registry_doesnt_add_up`, an entrance board answer); his solve-gated
  confrontation instead reports the too-unanimous 8 PM canvass (`unanimous_at_eight`, still
  neutral); the final board's WHEN-1 clause asks for the time
  "a full half hour before his own call", and the 7:00 call sits on the pool area's crumpled
  schedule. The subtraction is the player's to do.
- 6:45–6:47 PM — Coco sees (and accidentally photographs) a figure wheeling big black cases
  through the garden; he finds Rudebeak soggy and docile at the bird bath and seizes the moment.
  Basil, still hiding, witnesses it (straw hat at the photo's edge; his gated confession).
- 7:00 PM — Two equipment cases are signed in at the bedroom wing (`d_marsh_7pm` "Manifest
  Entry" + `time_700pm`, the scribbled signature split out as `scrawled_signature`) while the
  production schedule has D. Marsh in the library. Rosa's dinner cart is also in the wing at
  7:00 (duty roster, service tray) and the grips wheel lamp returns through at 7:20
  (`time_720pm`; Wexler mentions it too) — three sets of wheels in one hour. Derek (second
  floor) hears the heavy rumbles from DOWNSTAIRS - entrance hall, then living room - never
  "past his door" (playtest 6 geography fix), and everything happens "this evening", never
  "that night" (the whole case is tonight). His confrontation slip (`hall_rumble_no_soup`):
  the rumble came twice, same weight, and the kitchen only ran one cart - but he retreats to
  the squawk: "the bird was still AROUND at eight... right?" Rosa's slip (`cable_grease`)
  pins the second rumble to equipment, not lamps.
- Before lockdown — the cases are stashed behind the garden potting shed under a tarp
  (Sound-Proof Cases, found in the garden).
- 8:00 PM — Fake squawk: tiny speaker taped behind the living room couch (the couch is a
  walk-behind prop — the speaker interactable sits in the corridor behind it, hidden until
  the couch fades), fed by the audio
  cable from the library mixing board. Motive: 47 takes ruined by the bird (session log).
  TWO earwitnesses: Vivienne ("mid-affirmation") and Derek - it cut straight through his
  noise-canceling headphones (`derek_heard_squawk`), which is why he is SURE the bird was
  fine at eight. The bedroom board ends on that false conclusion; the forced
  bedroom-before-living-room order means the player carries it into the cage room and
  watches it break.
- 8:15 PM — Police lockdown. Only the lockdown clue ("Lockdown - 8:15 PM") carries the 8:15 timestamp.

**Deliberate pacing**: the early rooms (entrance, living room, bedroom) never name D. Marsh.
The registry lists the whole household; the manifest signature is a scrawl; the cage-side
evidence is a subtle cable plus suspect items from three characters (glove = Basil,
lemon polish cloth = Rosa, vocab ledger = Vivienne-blames-Rosa). WHO only firms up in the
library (badge, Wexler's "stopped complaining") and the pool area stitches the timeline
(sighting + 6:47 photo + schedule contradiction). Keep it that way — the playtest found
front-loaded Marsh evidence killed the mystery.

**Red herrings**: Basil (broke the latch, acts guilty, confesses only after the garden solve),
Rosa (motive note + cart at 7 PM + ambiguous feathers + polish cloth at the cage + house keys +
"maybe they are not wrong to look"), Derek (headphones with a spare *audio cable*/ultimatum),
Chip (PR panic — but he earns commission on the bird), Wexler (thrilled about the drama).

**Final board** (playtest 5: pure timeline reconstruction). "He was inside by [WHEN] - a full
half hour before his own call. The snatch was caught on camera at [WHEN], and by [WHEN] the
cargo was rolling right past the bedrooms..." Answers in parse order: `time_630pm`,
`time_647pm`, `time_700pm`, `d_marsh_sound_guy` (WHO), `sound_proof_cases` (WHAT),
`hidden_speaker` (HOW), `ruined_takes` (WHY). The WHERE/WHEN bank is exactly the eight macro
time chips, chronologically sorted - the finale IS sorting the evening. All final answers have
ungated sources.

**Final board near-misses**: WHEN-1 6:30 requires the half-hour arithmetic against the
schedule's 7:00 call (6:00 and 6:45 tempt); WHEN-2 6:47 vs 6:45 (the photo's exact timestamp
vs Coco's rounded sighting); WHEN-3 7:00 vs 7:20 (cases vs lamp returns - broken by the
manifest's readable names and Rosa's cable grease); WHO adds `derek_the_husband`, `the_maid`,
`the_gardener`, `knew_cases_empty` (Marsh's slip as a pseudo-suspect chip); WHAT/HOW add
`headphone_box`, `broken_latch`, `shears_match_latch`, `cable_grease`; WHY adds
`star_note_to_husband`, `insulted_cooking`, `basil_free_the_bird`, `pr_disaster`.

---

## Build & run

```bash
dotnet restore
dotnet run
```

Controls: WASD or arrow keys. Enter to interact/advance dialogue. In an interrogation menu:
W/S (or arrows) to choose a topic, Enter to act. Escape backs out one layer (menu -> dialogue ->
board -> autosave + main menu); it only quits the app from the main menu. F12 on the main menu
opens the DevMenu scene picker. F1 toggles the debug overlay.

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

Saves `debug_output/<case>_<room>[_<view>].png` after 1.5 s and exits.
- *(no view arg)* — the room with HUD, placeholders, debug rects.
- `journal` — opens the local deduction board with the room's clues unlocked.
- `final` — opens the final solve board with all macro clues unlocked.
- `notes` — opens the Case Notes page at its worst case: every room marked solved, recap =
  the room's raw local sentence template (layout check for casebook page 2).
- `dialogue` — opens the room's **longest** text segment (intro or topic response), fully typed
  (text-box fit check).
- `topics` — opens the room's fullest interrogation menu with all gated topics unlocked
  (worst-case menu layout check).
- `crt` — the plain room with the CRT overlay forced on.

Shell screens use the pseudo-case `ui` (no level is loaded):
```bash
dotnet run --no-build -- --screenshot ui menu         # main menu
dotnet run --no-build -- --screenshot ui settings     # settings screen
dotnet run --no-build -- --screenshot ui intro [page] # malibu poster intro, fully typed
dotnet run --no-build -- --screenshot ui end [beat]   # malibu end scene (beat 3 = rudebeak)
dotnet run --no-build -- --screenshot ui continue     # loads save.json exactly like CONTINUE
```

### Static level checker

```bash
python tools/verify_level.py            # malibu_mansion (default)
python tools/verify_level.py tutorial   # any case id under Content/Levels
```

Rooms are read from the case's own `rooms` list. Validates without launching the game:
interactable reachability vs collisions (cat feet box),
transfer ↔ spawn wiring (14 checks for Malibu), spawn clearance from transfer zones, config
consistency (keyword ids exist, local answers ungated-discoverable in-room, every clue
unlockable in its own room counting gated topics whose gates resolve in-room — cross-room-gated
confrontation clues WARN, `requiresSolve` rooms exist), final-board slot validity, and solve
balance (warns on any local slot whose category has no decoy in the room — single-option
auto-fill). Run it after any content change.

---

## Web build (KNI / WASM, itch.io)

`CatDetectiveWeb/` is a Blazor WebAssembly host project (KNI 4.2.9001) that
glob-includes all game sources; the desktop project is untouched. Game code
branches on `#if BLAZORGL` (defined only by the web csproj):
- JSON reads go through `Systems/GameFile.cs` (TitleContainer sync-XHR on web).
- `SaveSystem` persists to browser localStorage (`Systems/WebShims.cs` -
  `BrowserStorage`, initialized from `Pages/Index.razor.cs`).
- Music = raw mp3 via HTMLAudioElement (`BrowserAudio` + JS in
  `wwwroot/index.html`), started on the first user gesture (autoplay policy).
- The browser owns the canvas size: `SetCanvas` only resizes the render target,
  `Draw` letterbox-blits via `WasmScale`, and mouse coords are inverse-mapped.
- Screenshot mode, hot-reload, QUIT/Exit are compiled out on web.
- `GraphicsProfile.HiDef` is forced on web: KNI enforces the Reach 2048px
  texture cap (the 2100px cat walk sheets crash under Reach; desktop MonoGame
  never enforced it).
- Interactable highlight silhouettes are pre-rendered into render targets at
  LoadRoom (`InteractableEntity.PrewarmSilhouette`) - WebGL cannot GetData()
  from content textures.

Content: `Content/Content.Web.mgcb` is a copy of `Content.mgcb` with
`/platform:BlazorGL`, `/compress:True` + `/compression:Brotli` (decoded JS-side
via `wwwroot/js/decode.min.js` - the `window.BrotliDecode` global is required,
without it every content load throws), and no Song entry. **Keep its asset
entries in sync with Content.mgcb when adding art.** The desktop csproj
excludes it from MonoGame's MGCB via the `ExcludeWebContentReference` target.

Build / test / ship:
```bash
dotnet publish CatDetectiveWeb/CatDetectiveWeb.csproj -c Release -o CatDetectiveWeb/publish
python -m http.server 8321 --directory CatDetectiveWeb/publish/wwwroot   # local test
# zip the CONTENTS of publish/wwwroot (index.html at zip root) -> upload to itch.io
```
The `ItchIoFix` publish target renames `_framework`/`_content` (itch.io rejects
underscore paths), rewrites references, clears integrity hashes, and deletes
stale `.br`/`.gz` files. itch.io settings: HTML5 game, viewport 1456x816,
enable the fullscreen button. In `index.html`, the `nkast.Wasm.*.js` script
versions must match the resolved NuGet versions (8.0.11), and the rAF loop
schedules the next frame BEFORE invoking `TickDotNet` (the chain dies otherwise;
rAF also pauses in hidden tabs - the game freezing in background tabs is normal).

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
