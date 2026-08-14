# The Missing Macaw - Timeline & Solve Analysis

Companion to `script_narrative.md`. The question under analysis, from playtest: *the timeline
the player is supposed to trace is hard to find, and clues bundle multiple information points
(time + what), so the solves feel directed rather than deduced.*

Verified against the actual mechanics in `DeductionManager.cs` and the Pass 8 board rendering
in `Game1.cs` before drawing conclusions.

---

## 1. Ground truth: what the player actually sees on a board

These mechanical facts frame everything below:

- **Empty slots show only a category label** - `[ WHO ]`, `[ WHERE/WHEN ]` - in the tab color.
  The readable tags in the JSON (`[Lockdown]`, `[Heavy Cases]`) are never displayed; they only
  appear as a fallback in the filled-sentence recap. So the player's ONLY guidance is:
  (a) the prose around the slots, and (b) the chip names in the word bank.
- **Chips display the clue name**, timestamp included ("Heavy Cases - 7:00 PM").
- **The word bank is ordered by discovery**, filtered by tab. There is no chronological view
  anywhere in the game - not in the tabs, not on the Case Notes page. The timeline exists
  only in the player's head.
- **Local banks show every found clue in the room** (macro + micro). **The final bank shows
  macros only** - every micro timeline clue evaporates before the final solve.
- **Partial feedback** ("Incorrect logic - 2/3 fit") makes wrong submits informative.

Consequence: the deduction difficulty of any slot = (how much the surrounding prose narrows
it) x (how many same-category chips are in the bank). Both levers are currently set to "easy"
almost everywhere - the prose narrates the answer, and the banks are small.

---

## 2. Timeline audit: where each beat lives, and whether any solve ever needs it

The case's spine, beat by beat:

| Time | Beat | Clue (id) | Macro? | Ever required by a solve? |
|---|---|---|---|---|
| 5:45 | Basil comes indoors | `herb_run_545` | no | never (decoy only) |
| ~5:55 | Latch forced | `shears_match_latch` (no time in name) | yes | garden local |
| 6:00 | Bird already gone (snack untouched) | `missed_snack_6pm` | no | **never** |
| 6:30 | Crew column starts / someone early | `guest_registry` | yes | entrance local (as text-match) |
| 6:45 | Coco sees the cases | `eyewitness_645pm` (category: Who) | yes | pool local |
| 6:47 | Photo: figure + cases + straw hat | `selfie_figure_cases` | yes | pool local; final decoy |
| 6:55 | "B." supply run (manifest) | (no clue - manifest flavor only) | - | never |
| 7:00 | Cases signed into east wing | `d_marsh_7pm` | yes | bedroom local + final WHEN |
| 7:00 | Rosa's dinner cart | `dinner_cart_7pm` | no | kitchen local |
| ~7:00 | Derek hears wheels | `husband_hall_7pm` | no | bedroom local |
| 7:20 | Lamp returns | `lamp_returns_720` | no | **never** (decoy only) |
| pre-8:15 | Cases stashed behind shed | `sound_proof_cases` | yes | final WHAT |
| 8:00 | Fake squawk | `squawk_at_8pm` | yes | living room local; final decoy |
| 8:15 | Lockdown | `lockdown_815pm` | yes | entrance local |

Findings:

**2a. The timeline's two most important beats are unusable at the endgame.** The true time of
disappearance (6:00, `missed_snack_6pm`) and the disambiguation of the 7:00 wheels
(`dinner_cart_7pm`, `husband_hall_7pm`, `lamp_returns_720`) are all micro clues. The final
board's WhereWhen bank contains exactly five chips: Lockdown 8:15, Crew Arrivals 6:30,
Heavy Cases 7:00, 8:00 Squawk, Photo 6:47. The wheel-confusion the case so carefully builds
is simply absent from the final deduction - it was resolved once, in the bedroom local, by a
sentence that resolved it for you (see 4).

**2b. No solve ever asks the player to ORDER events.** Every WhereWhen slot asks "which clue
goes here", never "what happened first". The 6:47-photo-before-7:00-schedule contradiction -
the case's smoking gun - is stated by the pool solve sentence, not derived by the player. The
one solve that comes closest to a real timeline claim (pool) still spells out its own logic
("...crossed the garden BEFORE the [schedule] says setup even began").

**2c. Lamp returns is structurally forgettable, which matches the playtest.** It exists as one
bracketed line inside the manifest's six-entry text dump; no character ever mentions the
grips; it is never an answer; and its chip competes with two 7:00 chips while itself reading
"7:20". A decoy the player never has to eliminate is noise, not misdirection. Worse, it
quietly undermines the intended inference: Derek's solve-gated slip ("the hall rattled twice -
once with soup smell, once without") does not actually discriminate the cases from the 7:20
lamp run. As written, "the rumble without soup" fits the lamps just as well - the player is
never forced to rule them out (Rosa's cable-grease slip could do it, but nothing asks for it).

**2d. The 6:30 thread is a dead end wearing a macro badge.** Reyes's gated registry topic
("people drift in half an hour before anyone expects them") is the case's most elegant hint -
6:30 sign-in vs 7:00 call sheet = Marsh arrived early to do the snatch. But: it re-unlocks
`guest_registry` (no new notebook entry), the 30-minute arithmetic requires the pool-area
schedule the player may not have yet, and nothing anywhere ever asks the player to use it.
The 6:30 chip's only mechanical role is to LOSE to the manifest chip at the final board -
where it is eliminated by sentence text, not by timeline reasoning (see 4, Final).

---

## 3. Bundling audit: clues carrying multiple facts, and facts carrying no clue

### 3a. Multi-fact clues

The convention "put the time in the clue name" is right - the timeline should be legible on
the chips. The problem is when the name ALSO pre-fuses the time with its interpretation, so
the connection that should be the player's deduction ships pre-made:

| Clue | Facts fused | The deduction it pre-empts |
|---|---|---|
| `d_marsh_7pm` "Heavy Cases - 7:00 PM" | time + object + wing + illegible signature | "the 7:00 manifest entry WAS the cases" is the bedroom local's entire point - the chip name asserts it |
| `guest_registry` "Crew Arrivals - 6:30 PM" | crew column + start time + (via Reyes) the early-arrival anomaly | "someone came half an hour early" has no chip; the anomaly is fused into a clue named after the innocent reading |
| `squawk_at_8pm` "8:00 PM Squawk" | event + time + certainty ("everyone heard") | that only Vivienne attests to it (loose thread 4) has no representation |
| `eyewitness_645pm` "Coco's Sighting" (Who) | witness + time (6:45) + cargo (cases) | as a WHO chip it reads as evidence-about-a-person, not a person - on the final WHO bank it sits oddly beside "Derek the Husband" |
| `dinner_cart_7pm` "Dinner Cart - 7:00 PM" | who (R.) + time + destination (east wing) | acceptable fusion - this one is honest labeling |
| `production_schedule` "Production Schedule" | who (Marsh) + where (library) + when (7:00) | the alibi triple; fine as a document, but micro - so it cannot be laid against the photo at the final board |
| `missed_snack_6pm` "Missed Snack - 6:00 PM" | time + the inference "gone by six" | the case's true crime time, buried as a micro clue in one Petra topic |

The good counter-example already in the data: `selfie_figure_cases` vs `straw_hat_witness` -
one photograph, two clues, because it contains two facts pointing at two different people.
That is the pattern to extend.

### 3b. Facts with no clue at all - the confrontation slips are notebook-invisible

This may be the single biggest cause of "the timeline is hard to trace." Almost every gated
payoff - the case's best material - re-unlocks a clue the player already has, so the
notebook records NOTHING new:

| Confrontation slip | Currently maps to | What is lost |
|---|---|---|
| Derek: "the hall rattled TWICE - once with soup, once without" | `husband_hall_7pm` (already found) | the case-cracking fact of the bedroom, unrecorded |
| Reyes: "people drift in half an hour before their call sheet" | `guest_registry` (already found) | the 6:30 anomaly, unrecorded |
| Rosa: "the wing smelled of soup and cable grease" | `d_marsh_7pm` | the sensory link cart-vs-cases, unrecorded |
| Marsh: "the cases are EMPTY, both of them" | `sound_proof_cases` | that he KNEW, unrecorded |
| Basil: "I broke the latch... I saw the boxes roll by" | `shears_match_latch` + `eyewitness_645pm` | a full eyewitness confession, unrecorded |
| Coco: "at quarter to seven that man was doing cardio through the begonias" | `eyewitness_645pm` | the schedule-vs-sighting contradiction, unrecorded |

By the final board - which the confrontation lock guarantees is at least six conversations
after most of these - the player is reconstructing the 6:30 / 6:47 / 7:00 chain from spoken
dialogue held in memory, while the notebook shows them the same chips they had hours earlier.
The system that gates the finale on hearing the slips gives the slips no written afterlife.

---

## 4. Solve-by-solve leak audit

For each board: what the prose gives away per slot, the real decoy space (same-category chips
findable in the room), and what deduction remains.

### Entrance
"The [WhereWhen] sealed the mansion tight - Chip only fears a [Why], and the sign-in book
logs the [WhereWhen] from 6:30 PM on."
- Slot 1: bank = Lockdown 8:15 / Crew Arrivals 6:30 / North Gate Locked. "Sealed the mansion"
  is near-quoting Lockdown's flavor; North Gate is at least a live decoy. *Mild.*
- Slot 2: PR Disaster vs Chip's Commission. "Fears" does the work. *Mild.*
- Slot 3: "sign-in book... from **6:30 PM** on" vs a chip literally named "Crew Arrivals -
  **6:30 PM**". *Total giveaway - a string match.*

### Living room
"Everyone heard the [WhereWhen] - but the [What] and the [What] behind the couch tell a very
different story."
- Slot 1: 8:00 Squawk vs Missed Snack 6:00. "Heard" resolves it. The genuinely great pairing
  in this room - snack says GONE BY SIX, squawk says ALIVE AT EIGHT - is not what the
  sentence asks about; the snack clue is relegated to unused decoy. *Missed opportunity.*
- Slots 2-3: Latch / Speaker / Camera - three options, two slots, and "behind the couch"
  quotes the speaker's discovery text verbatim. *Mild-to-given.*

### Bedroom
"Derek shrugged off the [WhereWhen] as the dinner service - the [What] says the cart did
come - but the manifest also logged [WhereWhen] at seven."
- The intended deduction (two wheels, one hour) is fully narrated BY the sentence; the player
  confirms rather than discovers. "Manifest... at seven" uniquely selects "Heavy Cases -
  7:00 PM" (the rumble isn't "logged"; the lamps are 7:20). *The room's whole idea is
  spoiled by its own solve sentence.*
- Note this is the one room where the bank is genuinely rich (three 7-o'clock-ish WhereWhen
  chips) - the raw material for a real puzzle is already here.

### Kitchen
"[Who] wrote the [Why] note, skull and all - and the roster puts her [WhereWhen] in the
bedroom wing."
- "Wrote the note, skull and all" quotes the fridge note; "her" defeats the Herb Run decoy
  (initialed B.) before the player can consider it; Who bank is Rosa vs Ring of House Keys.
  *Near-autofill on all three slots.*

### Garden
"[Who] won't stop apologizing to the hedge, his [What] fit the snapped latch, his [What] has
lost its partner - and the [WhereWhen] say Rudebeak was loose in this garden tonight."
- Four slots, but "fit the snapped latch" IS the shears clue's content and "lost its partner"
  IS the glove's. The strongest What decoy in the game (Sound-Proof Cases) is in this bank
  and never gets a chance to tempt anyone. *Narrated.*

### Pool area
"[Who] and the [WhereWhen] agree: big dark cases crossed the garden before the [WhereWhen]
says setup even began."
- The best board in the case: it at least states a temporal claim (before). But "agree" +
  category colors resolve slots 1-2, and the claim itself - the contradiction - is asserted
  by the prose rather than assembled by the player. *The right idea, pre-chewed.*

### Final board
"[WHO] snatched Rudebeak from the garden and hid him using the [WHAT] - logged on the
manifest at [WHEN] - then faked the 8 PM squawk through a [HOW]. It all traces back to the
[WHY]."
- The template narrates the ENTIRE solution and leaves blanks for nouns: it hands the player
  the location (garden), the fact the squawk was faked, and the exact document the WHEN
  comes from. "Logged on the manifest" eliminates the 6:30 registry decoy, the 6:47 photo,
  the squawk, and the lockdown by document-matching, not by reasoning about when anything
  happened. The WHO slot is the only one that requires the accumulated investigation -
  and even there, the bank contains "Coco's Sighting" as a pseudo-person.
- Net effect: the case's finale never asks the one question the whole level teaches:
  *put these five timestamps in order and say which one is the crime.*

---

## 5. Recommendations

Ordered by leverage. 1-3 are pure content edits (JSON only); 4 is a small code change.

### R1. Give every confrontation slip its own clue (fixes 3b)
New micro/macro clues unlocked by the gated topics, so the notebook accumulates the
confrontation record and the late-game boards can use it:
- `hall_rumble_no_soup` (bedroom, WhereWhen, macro candidate): "Second Rumble - No Soup" -
  Derek's twice-fact. Name deliberately time-free: its POINT is that its time is unknown.
- `early_signin_630` (entrance, WhereWhen): "Early Sign-In - 6:30 PM" - Reyes's anomaly,
  replacing `guest_registry` as the registry's interesting chip (registry itself can stay as
  the innocent household roster).
- `cable_grease` (kitchen, What): Rosa's smell-memory - the fact that pins the no-soup rumble
  to sound equipment and finally lets the player eliminate the 7:20 lamps.
- `knew_cases_empty` (library, Who or What): Marsh knowing the cases are empty - the slip
  itself as evidence.
- Basil's confession already has physical anchors (shears, straw hat); optionally add
  `basil_saw_the_snatch` (garden, Who) so his eyewitness status is on record.
This also multiplies honest decoys/answers for R2-R3 without inventing new props.

### R2. Atomize the bundled clues (fixes 3a)
Split where a name currently pre-fuses time with interpretation:
- `d_marsh_7pm` -> keep id (final answer, save-compat) but rename toward the raw fact:
  "Manifest Entry - 7:00 PM" or "Two Cases Signed In - 7:00 PM"; move the illegible-signature
  fact to its own micro Who clue ("Scrawled Signature") so identity-of-the-signer becomes a
  question the player holds open.
- `guest_registry` -> demote the fused name; the 6:30 fact moves to `early_signin_630` (R1).
- `eyewitness_645pm` -> recategorize or rename: as a Who chip it should read as a person-fact
  ("Someone Hauled Cases - 6:45 PM" as WhereWhen, with Coco-as-witness a separate micro Who
  chip), so the final WHO bank contains only plausible culprits.
- Principle going forward: **time + raw observation in the name; interpretation never.**
  "Heavy Cases - 7:00 PM" says what the manifest says: fine. A name may not answer the
  question its room's board asks.

### R3. De-key the solve sentences (fixes 4)
Adopt two hard rules for every template, local and final:
1. **No quoting**: a sentence never contains a timestamp, document name, or distinctive noun
   that appears in an answer chip's name or discovery text ("6:30 PM", "manifest",
   "behind the couch", "skull").
2. **Pose the question, not the conclusion**: sentences should state a tension and let the
   chips resolve it, with word order fixing same-category slot order chronologically.
Sketches (illustrative, not final copy):
- Living room: "By the [WhereWhen] the bird was already gone - two hours before the
  [WhereWhen] everyone swears by. The cage did not open itself: the [What] proves that, and
  the [What] explains the rest." (Answers: Missed Snack 6:00 / 8:00 Squawk / Broken Latch /
  Hidden Speaker - promotes the snack clue from dead decoy to the room's thesis.)
- Bedroom: "Two rumbles passed the bedroom door. The first was the [WhereWhen]. The second
  was the [WhereWhen] - and Derek never looked up." (Cart / Cases, with Lamps 7:20 and the
  no-soup rumble as live decoys; the player must use times and Rosa's grease to order them.)
- Final: restructure as chronology - e.g. "It began when [WHEN-1], was caught on camera when
  [WHEN-2], and rolled past the bedrooms at [WHEN-3]. [WHO] hid Rudebeak in the [WHAT] and
  covered the silence with a [HOW] - all because of the [WHY]." Three ordered time slots
  drawn from the same five-chip WhereWhen bank turn the finale into exactly the deduction
  the playtest wanted: sort the timeline, identify the crime inside it. (Partial feedback
  makes a pure-permutation brute force possible; acceptable, or cap submits per visit.)

### R4. Give the timeline a home in the UI (fixes 2, small code change)
The chips already carry parseable times ("- 7:00 PM"). Either:
- sort the WhereWhen tab chronologically instead of by discovery order, or
- add a "Timeline" strip to the Case Notes page (Pass 8b): every found clue whose name
  carries a timestamp, listed in time order.
Zero new content required; directly answers "the timeline is difficult to find." The Case
Notes page is the natural home - it is already the investigation-status spread, and per the
conventions ("timetable clues are the good stuff") the payoff of collecting timed clues
should be *watching the evening assemble itself*.

### R5. Rehabilitate the lamp returns (fixes 2c)
Currently pure noise. Two options:
- Cheap: give it a voice - Coco complains about a third set of wheels, or Wexler mentions the
  grips striking lamps - so the player has heard of it before the manifest.
- Better: make it a live alternative hypothesis. Let Derek's no-soup rumble be vague on time
  ("around seven, maybe later"), so the 7:20 lamps genuinely fit it until Rosa's cable-grease
  clue (R1) rules them out. Then the bedroom/kitchen pair becomes a real
  eliminate-the-alternative deduction, and the lamps earn their place in memory.

### Interaction with the four loose threads (script_narrative.md)
- Thread 1 (Basil's 6:55 "B." manifest line) becomes MORE visible under R3/R4 - a
  chronological view will surface the contradiction; fix the manifest line first.
- Thread 4 (only Vivienne attests the squawk) intersects R2: if a second earwitness is added,
  keep the 8:00 chip singular - one clue, one fact, "the house believes 8:00."
