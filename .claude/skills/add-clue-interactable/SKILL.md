---
name: add-clue-interactable
description: Generates JSON for a new interactable object and its associated clues for Cat Detective's level_config.json. Use this skill when the user wants to add a new inspectable prop, scene object, or interactive element to a level, or when they describe a new clue and want the full JSON for both the clue entries and the interactable entry.
user_invocable: true
---

# Add Clue + Interactable

Generates ready-to-paste JSON snippets for `level_config.json` — both the `"clues"` entries and the matching `"interactables"` entry.

## The 3-Tier Information System

Every piece of information exists at three levels:

| Tier | Where it lives | Rule |
|------|---------------|------|
| **Keyword** (`displayText`) | Inside `keywords[]` of the interactable | Must match the bracketed text in `"text"` exactly (case-sensitive) |
| **Name** (`name`) | Inside the clue entry | 1–3 words; shown on the Deduction Board |
| **Context** (`context`) | Inside the clue entry | One sentence; shown in the Notebook |

## Clue Rules

- `id`: `snake_case`, unique across the level, descriptive of the fact (not the prop).
- `category`: one of `"Who"`, `"What"`, `"Why"`, `"WhereWhen"`.
  - **Who** — a person or suspect ("the housekeeper", "Lance's ex")
  - **What** — a stolen/relevant object or event ("broken trophy", "torn contract")
  - **Why** — a motive ("insurance fraud", "jealousy")
  - **WhereWhen** — a location or time ("Malibu Mansion", "Day Before Premiere")
- `name`: 1–3 words, Title Case. This is what the player reads on the Deduction Board card.
- `context`: One complete sentence. Explains *why* this clue matters or *where* it was found.

## Interactable Rules

- `id`: `inspect_<prop_name>` by convention.
- `text`: The dialogue string. Wrap each keyword in `[square brackets]`. The bracketed text is the keyword's `displayText` — it must match exactly.
- `keywords[]`: One entry per bracketed phrase in `text`.
  - `displayText`: copied verbatim from the brackets.
  - `id`: the `id` of the clue this keyword unlocks.
  - `color`: one of `"plot"` (story-critical), `"crime"` (directly criminal), `"misc"` (background detail).
- Optional display fields (omit if using defaults):
  - `scale` (float, default 1.0) — scales the dialogue box.
  - `align` — `"BottomCenter"` (default), `"Center"`, `"Left"`.
  - `offsetX` / `offsetY` (int) — pixel nudge from anchor position.

## Output Format

Always output two separate JSON blocks, clearly labelled, ready to paste.

### Block 1 — paste into `"clues": [...]`

```json
{ "id": "clue_id_here", "category": "What", "name": "Short Name", "context": "One sentence explaining this clue." },
{ "id": "second_clue",  "category": "WhereWhen", "name": "Place Name", "context": "Found in/on/near the prop." }
```

### Block 2 — paste into `"interactables": [...]`

```json
{
  "id": "inspect_prop_name",
  "text": "Dialogue text with [keyword one] and a [second keyword] inline.",
  "keywords": [
    { "displayText": "keyword one",   "id": "clue_id_here", "color": "plot"  },
    { "displayText": "second keyword","id": "second_clue",  "color": "crime" }
  ]
}
```

## Quality Checklist

Before outputting, verify:
- [ ] Every `[bracketed phrase]` in `text` has a matching entry in `keywords[]` with identical `displayText`.
- [ ] Every keyword `id` references a clue that exists (either new or already in the `"clues"` array).
- [ ] Each `name` is 1–3 words.
- [ ] Each `context` is exactly one sentence.
- [ ] No duplicate `id` values with existing clues in the level.
- [ ] `category` makes sense for the information type.
- [ ] `color` reflects importance: `"crime"` only for directly incriminating evidence.

## Example

**User prompt:** "Add a dusty bookshelf. The cat finds a love letter signed 'V' and a receipt for a boat rental in Malibu, dated last Friday."

**Output:**

Clues to add to `"clues": [...]`:
```json
{ "id": "love_letter_v",    "category": "Who",       "name": "Signed 'V'",       "context": "A love letter found in the bookshelf, signed only with the initial V." },
{ "id": "boat_rental",      "category": "WhereWhen", "name": "Boat Rental Malibu","context": "Receipt for a boat rental in Malibu, dated last Friday." }
```

Interactable to add to `"interactables": [...]`:
```json
{
  "id": "inspect_bookshelf",
  "text": "Dusty, but not untouched. A folded letter — signed [signed 'V']. Behind it, a crumpled receipt for a [boat rental in Malibu].",
  "keywords": [
    { "displayText": "signed 'V'",            "id": "love_letter_v", "color": "plot"  },
    { "displayText": "boat rental in Malibu", "id": "boat_rental",   "color": "plot"  }
  ]
}
```
