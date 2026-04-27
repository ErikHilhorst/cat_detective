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
- `name`: 1–3 words, Title Case. Shown on the Deduction Board card.
- `context`: Exactly one complete sentence. Shown in the Notebook.
- `inspectorDescription`: 1-2 sentences of extended lore/flavor text for the UI Inspector Panel.

### Interactable Rules (goes into `room_config.json`)
- `id`: `inspect_<prop_name>` by convention.
- `text`: The dialogue string. Wrap each keyword in `[square brackets]`. The bracketed text is the keyword's `displayText` — it must match exactly.
- `keywords[]`: One entry per bracketed phrase in `text`.
  - `displayText`: copied verbatim from the brackets (case-sensitive).
  - `id`: the `id` of the clue this keyword unlocks.
  - `color`: one of `"plot"` (story-critical), `"crime"` (directly criminal), `"misc"` (background detail).
- Optional display fields (omit if using defaults):
  - `scale` (float, default 1.0)
  - `align` — `"BottomCenter"` (default), `"Center"`, `"Left"`.

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
- Append a new Tiled object to its `"objects"` array.
- **Example format:**
  `{ "id": <generate_random_int_over_100>, "name": "inspect_bookshelf", "x": 500, "y": 500, "width": 64, "height": 64, "type": "", "visible": true, "rotation": 0 }`
  *(Place it at arbitrary coordinates like 500,500; the user will move it in Tiled).*
- **Write** the updated JSON back to the file.

---

## Quality Checklist
Before concluding your task, verify:
- [ ] Every `[bracketed phrase]` in `text` has a matching entry in `keywords[]` with identical `displayText`.
-[ ] Every keyword `id` references a clue that was just added to `case_config.json`.
- [ ] Each clue `name` is 1–3 words.
- [ ] Each clue `context` is exactly one sentence.
- [ ] The `roomId` in the clue exactly matches the current room you are editing.
- [ ] You have reminded the user to place the `.png` file in the room's `Interactables/` folder and add it to `Content.mgcb` with `PremultiplyAlpha=False`.