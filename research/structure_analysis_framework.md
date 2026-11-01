# Micro / Meso / Macro - structure analysis framework

A tool for checking whether a case holds together at three scales, and whether each
scale also works on its own. Designed for Cat Detective's systems (interactable ->
clue chip -> room board -> final board), tested first on The Missing Macaw.

- **Micro** = one interactable and the clue(s) it yields (the fact + its wrapping text).
- **Meso** = one room: its cast, its objects, its local board, its confrontation.
- **Macro** = the whole case: the culprit's case theory, the timeline, the final board.

The core idea: **the macro defines a small set of questions; each room owns one of them;
each object serves its room's question.** When the arrows between the levels work in
both directions (questions flow down, answers flow up), the case feels connected. When a
room answers a question the macro never asks, or the macro asks something no room
rehearsed, you get loose threads.

---

## 0. First, write the spine (macro, top-down)

You can only test the connections once the macro is written down as **propositions**, not
as a story. Three instruments:

### 0a. The MMO-T ledger (suspects x Means / Motive / Opportunity)

| Suspect | Means | Motive | Opportunity | What closes the gap (clears them) |
|---------|-------|--------|-------------|-----------------------------------|
| Culprit | clue @ room | clue @ room | clue @ room | - (all three hold) |
| Herring A | ... | ... | ... | the one fact that breaks a cell |

Rules:
- The culprit is the **only** suspect with all three cells filled **and none broken**.
- Every red herring has **two of three** filled convincingly, plus **one clean break**
  that the player can find (an alibi, a contradiction, a witness). A herring with all
  three and no break is a second culprit; a herring with one cell is just noise.
- Every cell names a **clue id and a room**. An empty source = a hole in the macro.
- The break of a herring is the natural payoff of that herring's **room confrontation**.

### 0b. The timeline lattice

One row per time chip: `time | event | source (room, interactable) | who it places where`.
Every event that the final board relies on needs (1) a source, (2) a pairing a player can
re-make on a board, and (3) at least one near-miss neighbour time.

### 0c. The Big Lie

Most good mysteries hinge on one **false belief** the culprit manufactures (here: "the
bird was alive at 8 PM"). Track it as three beats:
**planted** (where the player comes to believe it) -> **cracked** (first contradiction) ->
**demolished** (proof of how it was faked). Each beat should land in a specific room.

From these three instruments, derive **5-7 case questions** (e.g. "When did the bird
really leave?", "Who was inside early enough?", "How was the bird moved?", "How was the
house fooled?", "Why?"). These are the questions rooms get assigned.

---

## 1. Micro tests (per interactable / clue)

| # | Test | Pass condition |
|---|------|----------------|
| µ1 | **One fact** | The clue states one observable fact, evidence not conclusion. |
| µ2 | **Belongs here** | Why does this object exist in *this* room, near *these* people? It should be explainable by the room's place/cast (a duty roster in a kitchen, a manifest in a hall). |
| µ3 | **Role tag** | Exactly one primary role: `answer-local`, `answer-final`, `decoy` (for which slot), `herring-cell` (fills which suspect's M/M/O cell), `herring-break`, `lie-plant/crack/proof`, `bridge` (points to another room), `setup` (makes a later fact recognisable). `flavor` alone = fail. |
| µ4 | **Spine tag** | Touches at least one axis: Means, Motive, Opportunity, Timeline, Lie. A clue on no axis is a loose thread no matter how charming. |
| µ5 | **Hook** | Does the wrapping text name a person, place, object or time that exists elsewhere? (The connective tissue between micro and meso/macro.) |
| µ6 | **Readable alone** | Without context, does the player understand *what was found*? (Not what it means - that's the board's job.) |

## 2. Meso tests (per room)

Write a **Room Contract** before (re)writing a room's text:

```
Room:            kitchen
Case question:   <the one macro question this room owns>
Key suspect:     <who is tested here>  -> cells touched: M / M / O
Lie beat:        plant | crack | proof | none
Timeline input:  <events + times sourced here>
Final output:    <final-board chips sourced here, or near-misses it arms>
Turn:            before: player believes X  ->  after: player believes Y
Bridges in:      <facts from other rooms that this room recontextualises>
Bridges out:     <threads this room hands to another room>
Board:           answers the case question; last clause = the open question that sends the player on
Confrontation:   the herring break / culprit slip for the key suspect
```

| # | Test | Pass condition |
|---|------|----------------|
| R1 | **Stateable question** | The room's question fits in one sentence. If you can't write it, the room is a collection of objects. |
| R2 | **Board = question** | The local board's slots are the answer to *that* question, not a recap of what was found. A board that just re-states discovered facts is recall, not deduction. |
| R3 | **Coherence ratio** | Share of room clues whose role serves the room question (answer or decoy for it). Target >= 70%. |
| R4 | **Suspect focus** | One key suspect has a cell filled or broken here; the confrontation pays that off. |
| R5 | **Rehearses the macro** | At least one local answer is (or directly arms) a final-board answer, so the room solve visibly *moves the case*. |
| R6 | **Turn** | The room changes a belief (McKee: every scene turns a value). No turn = filler room. |
| R7 | **Bridges** | >= 1 bridge in and >= 1 bridge out, named in text the player actually reads. |
| R8 | **Ends on a question** | The board's last clause opens the next question (the herring suspicion, the next room). |

## 3. Macro tests (whole case)

| # | Test | Pass condition |
|---|------|----------------|
| C1 | **Ledger complete** | Every MMO cell has a source; culprit uniquely complete; every herring has one findable break. |
| C2 | **Question coverage** | Every case question is owned by >= 1 room; no room is unowned. |
| C3 | **Final traceability** | Every final slot traces to a room whose board rehearsed it (R5), not just a chip lying around. |
| C4 | **Lie arc** | Plant, crack and proof all exist, in that order along the forced/likely path. |
| C5 | **Information curve** | Along the likely room order, suspicion should rise on herrings early and converge on the culprit late (pacing: no Marsh in the first three rooms, by design). |
| C6 | **Final board = the theory** | The final sentence reads as the ledger's culprit row + the lie, so filling it feels like stating the case, not answering trivia. |
| C7 | **Herrings resolve** | Each herring ends with their own truth (Basil = the latch, Rosa = the cart, Derek = the ultimatum) so nothing hangs. |

## 4. Two-way check (do the levels connect?)

- **Top-down:** for each case question -> which room owns it -> which objects in that room
  carry it. A gap here = the macro asks something the meso never set up.
- **Bottom-up:** for each clue -> role -> which room question it serves -> which case
  question that feeds. A dead end here = a loose thread.

A simple matrix makes both visible: rows = clues, columns = case questions, cells = role.
Empty rows are loose clues; empty columns are unsupported questions.

## 5. Scoring

Score each test 0 (fails) / 1 (partly) / 2 (holds). Report per room (R1-R8, plus the
average micro score of its objects) and for the case (C1-C7). The useful output is not
the total but the **pattern**: e.g. strong micro + weak R5 = rooms that don't feed the
case, which is exactly the "loose threads" feeling.

Possible tooling later: add an optional author-only `spine` tag per clue in
`case_config.json` (e.g. `["opportunity:marsh", "lie:crack"]`) and let
`verify_level.py` print the matrix and the R5/C3 checks automatically.

---

## Appendix: first structural readout of The Missing Macaw

From configs only (no text read yet). This is a first pass, not the full analysis.

**R5 / C3 - do local boards rehearse the final board?**

| Room | Final answers sourced in room | Rehearsed on its local board? |
|------|-------------------------------|-------------------------------|
| entrance | `time_630pm` (registry) | **No**: board asks about lockdown 8:15 / still inside / Chip; 6:30 never on the board |
| living_room | `hidden_speaker` | **No**: board is about squawk vs missed snack + latch |
| bedroom | `time_700pm` (manifest) | Yes (`d_marsh_7pm`, `time_700pm`) |
| library | `d_marsh_sound_guy`, `ruined_takes` | Yes |
| kitchen | none (only decoys `insulted_cooking`, `cable_grease`) | n/a: room feeds no final answer |
| garden | `sound_proof_cases` | **No**: board is about Basil, cases absent |
| pool_area | `time_647pm` | Yes |

So only 3 of 7 rooms rehearse a final answer, and the kitchen outputs nothing to the
final board. That matches the playtest feeling that room deductions hang loose.

**Observations to verify in the full pass:**
- Most local answers are micro clues (people, events). Only ~9 local answers are macro.
  The room boards and the final board live on different chip sets, so a room solve
  rarely feels like progress on the case.
- **Lie arc (C4):** plant = bedroom (Derek's 8 PM squawk) -> crack = living room (missed
  6:00 snack) -> proof = speaker (living room) + cable (library). The final board's HOW
  slot asks for the speaker, but no board asks "when did the bird really leave?" The
  6:00 departure, the strongest deduction in the case, isn't asked on the final board.
- **Ledger (C1) sketch:** Marsh M = cases (garden) + speaker/cable, Mo = ruined takes
  (library), O = 6:30 (entrance) + 6:47 (pool) + 7:00 (bedroom). Basil looks like a
  near-full row (shears = means, "cages are prisons" = motive, before-6 = opportunity),
  and his break (never opened the door, hid behind the shed, witnessed the snatch) is
  only reachable after the garden solve. Check that the break is legible. Rosa's means
  cell (the cart) and Derek's opportunity cell look thin, so check them.
- **Kitchen** looks like the weakest meso unit: its board is about the 5:45 herb run and
  the 7:00 cart, and neither connects to a case question beyond "Rosa was busy". Candidate
  owner of a question such as "what rolled through the wing at 7:00?" (one cart vs. the
  second rumble).
- **Entrance** works because its question is clear ("is the thief still inside, and whose
  evening doesn't add up?") and Reyes asks it out loud. Note that its board still never
  asks for 6:30, the entrance's real macro output.
