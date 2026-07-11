---
name: add-clue-interactable
description: Actively writes a new interactable object into the game. It uses Read/Write tools to update case_config.json (for clues), room_config.json (for dialogue), and room_map.json (to place the object in Tiled).
user_invocable: true
---

# Add Clue + Interactable (Multi-Room Auto-Writer)

This skill automates adding an inspectable object to a specific room. **You will use your Read and Write tools to modify the 3 required files directly.**

## The Information System & Rules

Every piece of information exists at multiple levels. Follow these strict rules when generating the content:

### Clue Rules (goes into `case_config.json`)
- `id`: `snake_case`, unique across the case, descriptive of the fact (not the prop).
- `roomId`: The exact string ID of the room this clue is found in.
- `isMacroClue`: `false` (unless the user explicitly states this is a final case-level macro clue).
- `category`: strictly one of `"Who"`, `"What"`, `"Why"`, `"WhereWhen"`.
  - **Who** — a person or suspect ("the housekeeper", "Lance's ex")
  - **What** — a stolen/relevant object or event ("broken trophy", "torn contract")
  - **Why** — a motive ("insurance fraud", "jealousy")
  - **WhereWhen** — a location or time ("Malibu Mansion", "Day Before Premiere")
- `name`: short, Title Case. Shown on the Deduction Board card. If the clue carries a time, put it
  in the name ("Dinner Cart - 7:00 PM") so the board reads as a timeline. Include character names
  where relevant ("Rosa the Maid"). Never reuse a timestamp already used by an unrelated clue.
- `context`: Exactly one complete sentence. Shown in the Notebook.
- `inspectorDescription`: 1-2 sentences for the UI Inspector Panel, in the cat's voice.
- **Describe evidence, not conclusions.** State facts and let the player connect them. Never write
  the deduction into the clue ("this proves he was already inside" is forbidden; "sealed at
  8:15 PM" is right).
- **Category balance**: check the room's existing clues — avoid piling a 4th clue onto a tab while
  other tabs sit empty.
- **Decoys are welcome**: an `isMacroClue: false` clue that shares a category with a real answer
  pads the word bank and raises difficulty. Only `isMacroClue: true` clues appear on the final
  board's word bank.
- **ASCII only** — the sprite font covers ASCII 32–126. No em-dashes (use ` - `), no curly quotes,
  no ✓. This applies to every string in all three files.

### Interactable Rules (goes into `room_config.json`)
- `id`: `inspect_<prop_name>` by convention.
- `text`: The dialogue string. Wrap each keyword in `[square brackets]`. The bracketed text is the keyword's `displayText` — it must match exactly.
- `keywords[]`: One entry per bracketed phrase in `text`.
  - `displayText`: copied verbatim from the brackets (case-sensitive).
  - `id`: the `id` of the clue this keyword unlocks.
  - `color`: one of `"plot"` (story-critical), `"crime"` (directly criminal), `"misc"` (background detail).
- Optional display fields (omit if using defaults):
  - `texture` — fallback content path used when no per-name sprite exists under the room's
    `Interactables/` folder. Until real art lands, always set `"Shared/placeholder_person"`
    (people, `scale` 0.15) or `"Shared/placeholder_object"` (objects, `scale` 0.07–0.13 by size).
  - `scale` (float, default 1.0)
  - `align` — `"BottomCenter"` (default), `"Center"`, `"Left"`.

### Topic Rules (characters only — `topics[]` in the interactable)
Objects use a single `text`. **Characters** additionally get an interrogation menu:
`text` becomes the intro (first meeting beat), and `topics[]` lists 3-4 selectable entries.
Characters also get a `name` field ("Basil", "Officer Reyes") — it feeds the gate-unlock
toast ("Basil (Garden) might have some explaining to do..."). Use a non-spoiler name if the
character's real name is itself a reveal (D. Marsh is "The Sound Guy").
- `prompt`: a **cat action**, not a question — the detective cannot talk ("Stare at him. Do not
  blink.", "Sit hopefully beneath the treat jar"). One line, distinct per character; never reuse
  a generic "ask about X" phrasing across characters.
- `text`: the character's monologue in response (humans love explaining themselves to a cat).
  Pages split on `|`; same `[keyword]` rules as the intro.
- `keywords[]`: unlocked when the topic is chosen, not on approach. Local deduction answers must
  be reachable via the intro or an **ungated** topic (or an object) in the same room.
- `requiresClue` (optional): clue id that must be found before the topic appears. Use it for
  confrontation beats — reacting to evidence the player has found. Gated topics are for bonus
  depth, red-herring resolution, or incriminating slips; they must **never be the only source of
  any clue**. Every keyword id used in a gated topic must also be unlockable ungated in the
  clue's own room (so the room clue counter can always complete without backtracking).
  `verify_level.py` enforces this.

---

## The 3 Files to Update

When the user gives a prompt like: *"Use add-clue-interactable. In `malibu_mansion` `living_room`, add a dusty bookshelf with a love letter signed 'V'."*

### 1. The Global Clues (`Content/Levels/<case_id>/case_config.json`)
- **Read** the file.
- Add the new clue(s) to the `"clues"` array.
- **Example format:** 
  `{ "id": "love_letter_v", "category": "Who", "roomId": "living_room", "isMacroClue": false, "name": "Signed 'V'", "context": "A love letter found in the bookshelf, signed with a V.", "inspectorDescription": "The handwriting is hurried, smelling faintly of expensive perfume." }`
- **Write** the updated JSON back to the file.

### 2. The Local Dialogue (`Content/Levels/<case_id>/<room_id>/room_config.json`)
- **Read** the file.
- Add the new interactable to the `"interactables"` array.
- **Example format:**
  `{ "id": "inspect_bookshelf", "text": "Dusty, but not untouched. A folded letter — [signed 'V'].", "keywords":[ { "displayText": "signed 'V'", "id": "love_letter_v", "color": "plot" } ] }`
- **Write** the updated JSON back to the file.

### 3. The Tiled Map (`Content/Levels/<case_id>/<room_id>/room_map.json`)
- **Read** the file.
- Find the layer where `"name": "Interactables"`.
- Append a new Tiled object to its `"objects"` array, using the map's top-level `nextobjectid` as
  the object `id`, then increment `nextobjectid`.
- **Example format:**
  `{ "id": <nextobjectid>, "name": "inspect_bookshelf", "x": 500, "y": 500, "width": 90, "height": 70, "type": "", "visible": true, "rotation": 0 }`
  Typical sizes: people ~100×140, objects ~90×70, small items ~60×50.
- **Place it deliberately**, not arbitrarily: on/near the furniture it belongs to in the background
  art, clear of `Transfers` zones and `Collisions` rects (the cat must be able to reach it —
  never edit the Collisions layer itself).
- **Write** the updated JSON back to the file.

### 4. Verify
- Run `python tools/verify_level.py` from the repo root — it checks reachability, keyword→clue id
  wiring, and deduction-board consistency.
- Optionally capture `dotnet run --no-build -- --screenshot <case_id> <room_id>` and check the
  placement in `debug_output/`.

---

## Quality Checklist
Before concluding your task, verify:
- [ ] Every `[bracketed phrase]` in `text` has a matching entry in `keywords[]` with identical `displayText`.
- [ ] Every keyword `id` references a clue that exists in `case_config.json`.
- [ ] Each clue `context` is exactly one sentence; descriptions state evidence, not conclusions.
- [ ] The `roomId` in the clue exactly matches the current room you are editing.
- [ ] All strings are pure ASCII (no em-dashes, curly quotes, or check marks).
- [ ] `python tools/verify_level.py` passes.
- [ ] If no `texture` fallback was set: remind the user to place the `.png` in the room's `Interactables/` folder and add it to `Content.mgcb` with `PremultiplyAlpha=False`.